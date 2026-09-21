using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Roblox.Dto.Users;
using Roblox.Exceptions.Services.Users;
using Roblox.Models;
using Roblox.Services.Exceptions;
using Roblox.Services.Users.Models;
using Roblox.Services.Users.Services;
using Roblox.Web.Infrastructure.Controllers;
using Roblox.Web.Infrastructure.Metadata;

namespace Roblox.Services.Users.Controllers;

[ApiController]
[InternalServiceOnly]
[Route("/v1")]
[Route("/apisite/users/v1")]
public class UsersController : RobloxControllerBase
{
    private readonly StaffPermissionsResolver _staffPermissions;

    public UsersController(StaffPermissionsResolver staffPermissions)
    {
        _staffPermissions = staffPermissions;
    }

    [RequireRobloxSession]
    [HttpGet("users/authenticated")]
    public async Task<AuthenticatedUserResponse> GetMySession()
    {
        var permissions = (await _staffPermissions.GetPermissionsAsync(safeUserSession.userId, services)).ToList();
        return new AuthenticatedUserResponse
        {
            id = safeUserSession.userId,
            name = safeUserSession.username,
            displayName = safeUserSession.username,
            isStaff = permissions.Any(),
            permissions = permissions.Select(permission => permission.ToString()).ToList(),
        };
    }

    [AllowRobloxAnonymous]
    [HttpPost("users/{username}/details")]
    [HttpGet("users/{username}/details")]
    public async Task<UserDetailsByUsernameResponse> GetUserByUsername(string username)
    {
        var result = (await services.users.MultiGetUsersByUsername(new[] { username })).ToList();
        var info = await services.users.GetUserById(result[0].id);
        var canViewInventory = !info.IsDeleted() && await services.inventory.CanViewInventory(info.userId);
        var inventoryRap = canViewInventory ? await services.inventory.GetInventoryRap(info.userId) : 0;

        return new UserDetailsByUsernameResponse
        {
            id = info.userId,
            name = info.username,
            displayName = info.username,
            description = info.description,
            created = info.created,
            isBanned = info.IsDeleted(),
            isInventoryPublic = canViewInventory,
            hasVerifiedBadge = info.isVerified,
            totalPlaceVisits = await services.games.GetTotalVisitsFromUser(info.userId),
            friendshipCount = await services.friends.CountFriends(info.userId),
            followingCount = await services.friends.CountFollowings(info.userId),
            followerCount = await services.friends.CountFollowers(info.userId),
            inventoryRap = inventoryRap,
        };
    }

    [AllowRobloxAnonymous]
    [HttpPost("users/{userId:long}")]
    [HttpGet("users/{userId:long}")]
    public async Task<UserDetailsByIdResponse> GetUserById(long userId)
    {
        var info = await services.users.GetUserById(userId);
        var canViewInventory = !info.IsDeleted() && await services.inventory.CanViewInventory(info.userId);
        var inventoryRap = canViewInventory ? await services.inventory.GetInventoryRap(info.userId) : 0;

        return new UserDetailsByIdResponse
        {
            description = info.description,
            created = info.created,
            isBanned = info.IsDeleted(),
            hasVerifiedBadge = info.isVerified,
            id = info.userId,
            name = info.username,
            displayName = info.username,
            inventoryRap = inventoryRap,
        };
    }

    [AllowRobloxAnonymous]
    [HttpPost("users")]
    public async Task<RobloxCollection<MultiGetEntry>> MultiGetUsersById([Required, FromBody] MultiGetRequest request)
    {
        var ids = request.userIds.ToList();
        if (ids.Count is > 200 or < 1)
        {
            throw new RobloxException(400, 0, "Invalid IDs");
        }

        var result = await services.users.MultiGetUsersById(ids);
        return new RobloxCollection<MultiGetEntry>
        {
            data = result,
        };
    }

    [AllowRobloxAnonymous]
    [HttpPost("usernames/users")]
    public async Task<RobloxCollection<MultiGetEntry>> MultiGetUsersByUsername([Required, FromBody] MultiGetByNameRequest request)
    {
        var names = request.usernames.ToList();
        if (names.Count is > 200 or < 1)
        {
            throw new RobloxException(400, 0, "Invalid Usernames");
        }

        var result = await services.users.MultiGetUsersByUsername(names);
        return new RobloxCollection<MultiGetEntry>
        {
            data = result,
        };
    }

    [AllowRobloxAnonymous]
    [HttpGet("users/{userId:long}/status")]
    public async Task<UserStatusResponse> GetUserStatus([Required] long userId)
    {
        var result = await services.users.GetUserStatus(userId);
        return new UserStatusResponse
        {
            status = string.IsNullOrEmpty(result.status) ? null : result.status,
        };
    }

    [RequireRobloxSession]
    [RequireRobloxCsrf]
    [HttpPatch("users/{userId:long}/status")]
    public async Task SetUserStatus([Required, FromBody] SetStatusRequest request)
    {
        try
        {
            await services.users.SetUserStatus(safeUserSession.userId, services.filter.FilterText(request.status));
        }
        catch (Exception e) when (e is StatusTooLongException or StatusTooShortException)
        {
            throw new RobloxException(400, 2, "Invalid request");
        }
    }

    [AllowRobloxAnonymous]
    [HttpGet("users/{userId:long}/username-history")]
    public async Task<RobloxCollectionPaginated<PreviousUsernameResponse>> GetPreviousUsernames(
        [Required] long userId,
        int limit = 100,
        string? cursor = null)
    {
        var userInfo = await services.users.GetUserById(userId);
        if (userInfo.IsDeleted())
        {
            throw new RobloxException(400, 0, "User is invalid or does not exist");
        }

        var entries = (await services.users.GetPreviousUsernames(userId))
            .Select(username => new PreviousUsernameResponse
            {
                name = username.username,
            });

        return new RobloxCollectionPaginated<PreviousUsernameResponse>
        {
            data = entries,
        };
    }
}
