using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Roblox.Dto.Users;
using Roblox.Services.Exceptions;
using Roblox.Website.Filters;

namespace Roblox.Website.Pages.Internal;

public class CollectibleInventory : RobloxPageModel
{
    [BindProperty(SupportsGet = true)]
    public long userId { get; set; }
    [BindProperty(SupportsGet = true)]
    public int pageIndex { get; set; } = 0;
    public List<CollectibleItemEntry> inventory { get; set; }
    public string username { get; set; }
    public string? errorMessage { get; set; }
    public long totalRap { get; set; }
    public int totalPages { get; set; }

    public async Task OnGet()
    {
        UserInfo info;
        try
        {
            info = await services.users.GetUserById(userId);
            username = info.username;
        }
        catch (RecordNotFoundException)
        {
            errorMessage = "User ID is invalid or does not exist.";
            return;
        }
        bool isStaff = await StaffFilter.IsStaff(userSession?.userId ?? 0);
        bool canViewInventory = await services.inventory.CanViewInventory(userId, userSession?.userId ?? 0);

        bool isAccountOk = info.accountStatus == Models.Users.AccountStatus.Ok;

        if ((!canViewInventory || !isAccountOk) && !isStaff)
        {
            errorMessage = "You don't have permissions to view the specified user's inventory";
            return;
        }

        inventory = new ();
        var offset = 0;
        while (true)
        {
            var results = (await services.inventory.GetCollectibleInventory(userId, null, "asc", 100, offset)).ToArray();
            if (results.Length == 0) break;
            offset += 100;
            inventory.AddRange(results);
        }

        foreach (var item in inventory)
        {
            totalRap += item.recentAveragePrice;
        }

        const int pageSize = 24;
        totalPages = (int)Math.Ceiling((double)inventory.Count / pageSize);
        pageIndex = Math.Max(0, Math.Min(pageIndex, totalPages - 1));
        inventory = inventory.Skip(pageIndex * pageSize).Take(pageSize).ToList();
    }
}