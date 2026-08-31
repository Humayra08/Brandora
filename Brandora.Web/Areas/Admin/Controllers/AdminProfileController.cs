using Brandora.Web.Data;
using Microsoft.AspNetCore.Mvc;

namespace Brandora.Web.Areas.Admin.Controllers;

public class AdminProfileController(ApplicationDbContext db) : AdminControllerBase(db)
{
    public async Task<IActionResult> Index()
    {
        await LoadAdminChromeAsync();
        ViewData["ActiveNav"] = "";
        ViewData["Title"] = "My Profile";

        var lastLoginClaim = User.FindFirst("LastLoginUtc")?.Value;
        ViewData["LastLoginUtc"] = lastLoginClaim is not null && DateTime.TryParse(lastLoginClaim, out var lastLogin)
            ? lastLogin
            : (DateTime?)null;

        return View();
    }
}
