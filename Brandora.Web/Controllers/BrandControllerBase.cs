using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

[Authorize]
public abstract class BrandControllerBase(UserManager<ApplicationUser> userManager, ApplicationDbContext db) : Controller
{
    protected async Task<BrandProfile?> GetCurrentBrandAsync()
    {
        var userId = userManager.GetUserId(User);
        var brand = await db.BrandProfiles.FirstOrDefaultAsync(b => b.UserId == userId);

        if (brand is not null)
        {
            ViewData["CompanyName"] = brand.CompanyName;
            ViewData["UnreadNotifications"] = await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
            ViewData["ProfilePictureUrl"] = brand.ProfilePictureUrl;
            ViewData["ContactFullName"] = brand.ContactFullName;
            ViewData["Email"] = await db.Users.Where(u => u.Id == userId).Select(u => u.Email).FirstOrDefaultAsync();
        }

        return brand;
    }
}
