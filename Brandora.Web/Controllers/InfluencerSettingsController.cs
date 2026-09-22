using Brandora.Web.Data;
using Brandora.Web.Models.Dashboard;
using Brandora.Web.Models.Domain;
using Brandora.Web.Models.Settings;
using Brandora.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

public class InfluencerSettingsController(UserManager<ApplicationUser> userManager, ApplicationDbContext db, MediaUploadService mediaUploads) : InfluencerControllerBase(userManager, db)
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
            Notifications = await db.Notifications.AsNoTracking()
                .Where(n => n.UserId == influencer.UserId)
                .OrderByDescending(n => n.CreatedAt).Take(5).ToListAsync(),
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
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Update(InfluencerProfileFormModel form)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null)
        {
            return RedirectToAction("Index", "Home");
        }

        if (form.RemoveProfilePicture && influencer.ProfilePictureUrl is not null)
        {
            mediaUploads.DeleteMedia(influencer.ProfilePictureUrl);
            influencer.ProfilePictureUrl = null;
        }

        if (form.ProfilePictureFile is { Length: > 0 })
        {
            var (url, error) = await mediaUploads.SaveProfilePictureAsync(form.ProfilePictureFile);
            if (error is not null)
            {
                TempData["ProfileErrors"] = error;
                return RedirectToAction("Index");
            }

            mediaUploads.DeleteMedia(influencer.ProfilePictureUrl);
            influencer.ProfilePictureUrl = url;
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (await GetCurrentInfluencerAsync() is null) return RedirectToAction("Index", "Home");
        if (!ModelState.IsValid)
        {
            TempData["PasswordMessage"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return Redirect("/InfluencerSettings#security");
        }
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        var result = await userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        TempData["PasswordMessage"] = result.Succeeded
            ? "Your password has been updated."
            : string.Join(" ", result.Errors.Select(e => e.Description));
        return Redirect("/InfluencerSettings#security");
    }
}
