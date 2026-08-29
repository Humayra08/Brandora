using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

[Authorize]
public abstract class InfluencerControllerBase(UserManager<ApplicationUser> userManager, ApplicationDbContext db) : Controller
{
    protected async Task<InfluencerProfile?> GetCurrentInfluencerAsync()
    {
        var userId = userManager.GetUserId(User);
        var influencer = await db.InfluencerProfiles.FirstOrDefaultAsync(i => i.UserId == userId);

        if (influencer is not null)
        {
            ViewData["AppSection"] = "Influencer";
            ViewData["CompanyName"] = influencer.FullName;
            ViewData["UnreadNotifications"] = await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
        }

        return influencer;
    }
}
