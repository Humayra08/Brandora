using Brandora.Web.Data;
using Brandora.Web.Models.Dashboard;
using Brandora.Web.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Brandora.Web.Controllers;

public class InfluencerSettingsController(UserManager<ApplicationUser> userManager, ApplicationDbContext db) : InfluencerControllerBase(userManager, db)
{
    public async Task<IActionResult> Index()
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var user = await userManager.GetUserAsync(User);

        var vm = new InfluencerSettingsViewModel
        {
            Profile = influencer,
            Form = new InfluencerProfileFormModel
            {
                FullName = influencer.FullName,
                UserName = user?.UserName,
                Email = user?.Email,
                PhoneNumber = user?.PhoneNumber,
                Location = influencer.Location,
                ContentNiche = influencer.ContentNiche,
                Bio = influencer.Bio,
                WebsiteUrl = influencer.WebsiteUrl
            }
        };

        if (TempData["ProfileSaved"] is not null)
        {
            ViewData["ProfileSaved"] = true;
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(InfluencerProfileFormModel form)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null)
        {
            return RedirectToAction("Index", "Home");
        }

        influencer.FullName = form.FullName;
        influencer.Location = form.Location;
        influencer.ContentNiche = form.ContentNiche;
        influencer.Bio = form.Bio;
        influencer.WebsiteUrl = form.WebsiteUrl;

        var user = await userManager.GetUserAsync(User);
        if (user is not null)
        {
            user.PhoneNumber = form.PhoneNumber;
        }

        await db.SaveChangesAsync();

        TempData["ProfileSaved"] = "true";
        return RedirectToAction("Index");
    }
}
