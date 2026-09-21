namespace Roblox.Services.Users.Models;

public sealed class AuthenticatedUserResponse
{
    public long id { get; set; }
    public string name { get; set; } = string.Empty;
    public string displayName { get; set; } = string.Empty;
    public bool isStaff { get; set; }
    public List<string> permissions { get; set; } = new();
}

public sealed class UserDetailsByUsernameResponse
{
    public long id { get; set; }
    public string name { get; set; } = string.Empty;
    public string displayName { get; set; } = string.Empty;
    public string description { get; set; } = string.Empty;
    public DateTime created { get; set; }
    public bool isBanned { get; set; }
    public bool isInventoryPublic { get; set; }
    public bool hasVerifiedBadge { get; set; }
    public long totalPlaceVisits { get; set; }
    public long friendshipCount { get; set; }
    public long followingCount { get; set; }
    public long followerCount { get; set; }
    public long inventoryRap { get; set; }
}

public sealed class UserDetailsByIdResponse
{
    public string description { get; set; } = string.Empty;
    public DateTime created { get; set; }
    public bool isBanned { get; set; }
    public bool hasVerifiedBadge { get; set; }
    public long id { get; set; }
    public string name { get; set; } = string.Empty;
    public string displayName { get; set; } = string.Empty;
    public long inventoryRap { get; set; }
}

public sealed class UserStatusResponse
{
    public string? status { get; set; }
}

public sealed class PreviousUsernameResponse
{
    public string name { get; set; } = string.Empty;
}
