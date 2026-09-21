using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Roblox.Dto.Users;
using Roblox.Models.Users;

namespace Roblox.Website.Pages.Internal;

public class Membership : RobloxPageModel
{
    public MembershipType existingMembershipType { get; set; }
    public string? successMessage { get; set; }
    public string? errorMessage { get; set; }
    public async Task OnGet()
    {
        if (userSession == null)
            return;
        var mem = await services.users.GetUserMembership(userSession.userId);
        existingMembershipType = mem?.membershipType ?? MembershipType.None;
    }

    [BindProperty]
    public MembershipType membershipType { get; set; }
    public async Task OnPost()
    {
        if (!Enum.IsDefined(membershipType) || userSession is null)
            return;

        if (membershipType is MembershipType.TurboBuildersClub)
        {
            errorMessage = "Turbo Builders Club is no longer available.";
            var mem = await services.users.GetUserMembership(userSession.userId);
            existingMembershipType = mem?.membershipType ?? MembershipType.None;
            return;
        }

        if (membershipType is MembershipType.OutrageousBuildersClub or MembershipType.Premium)
        {
            var totalPlaytime = await services.games.GetTotalPlaytimeSeconds(userSession.userId);
            if (totalPlaytime < 21600)
            {
                var remainingHours = Math.Ceiling((21600 - totalPlaytime) / 3600.0);
                errorMessage = $"You need at least 6 hours of total playtime. You still need {remainingHours} more hours";
                var mem = await services.users.GetUserMembership(userSession.userId);
                existingMembershipType = mem?.membershipType ?? MembershipType.None;
                return;
            }
        }

        await services.users.InsertOrUpdateMembership(userSession.userId, membershipType);
        var metadata = MembershipMetadata.GetMetadata(membershipType);
        successMessage = "Membership successfully changed to " + metadata.displayName + ". You will now receive "  + metadata.dailyRobux + " Robux each day.";
        existingMembershipType = membershipType;
    }
}