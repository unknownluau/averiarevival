using Microsoft.AspNetCore.Mvc;
using Roblox.Models.Sessions;
using Roblox.Models.Users;
using Roblox.Services;

namespace Roblox.Website.Pages.Internal;

public class Year : RobloxPageModel
{
    [BindProperty]
    public WebsiteYear year { get; set; }
    private UserSession? session => (UserSession?) HttpContext.Items[".AVERISECURITY"];
    public WebsiteYear currentYear { get; set; }
    
    public string? successMessage { get; set; }

    public async Task<IActionResult> OnGet()
    {
        if (session == null) return Redirect("/login");
        currentYear = await services.users.GetYear(session.userId);

        return Page();
    }

    public async Task OnPost()
    {
        if (session == null) return;
        await services.users.SetYear(session.userId, year);
        currentYear = year;
        successMessage = "Year updated.";
    }
}
