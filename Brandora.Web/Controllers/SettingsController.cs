using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Brandora.Web.Models.Settings;
using Brandora.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

public class SettingsController(UserManager<ApplicationUser> userManager, ApplicationDbContext db, MediaUploadService mediaUploads) : BrandControllerBase(userManager, db)
{
    public async Task<IActionResult> Index()
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var user = await userManager.GetUserAsync(User);
        ViewData["Email"] = user?.Email;

        var userId = userManager.GetUserId(User)!;
        var prefs = await db.NotificationPreferences.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);

        var payments = await db.Payments
            .Where(p => p.Collaboration.Campaign.BrandProfileId == brand.Id)
            .ToListAsync();

        return View(new BrandSettingsViewModel
        {
            CompanyName = brand.CompanyName,
            ContactFullName = brand.ContactFullName,
            WebsiteUrl = brand.WebsiteUrl,
            Industry = brand.Industry,
            MonthlyBudget = brand.MonthlyBudget,
            ExistingProfilePictureUrl = brand.ProfilePictureUrl,
            NotificationPreferences = prefs is null
                ? new NotificationPreferencesFormViewModel { NewProposals = true, ProposalUpdates = true, Messages = true, MilestoneUpdates = true, PaymentUpdates = true, CampaignDeadlines = true }
                : new NotificationPreferencesFormViewModel
                {
                    NewProposals = prefs.NewProposals,
                    ProposalUpdates = prefs.ProposalUpdates,
                    Messages = prefs.Messages,
                    MilestoneUpdates = prefs.MilestoneUpdates,
                    PaymentUpdates = prefs.PaymentUpdates,
                    CampaignDeadlines = prefs.CampaignDeadlines
                },
            TotalFunded = payments.Sum(p => p.Amount),
            PendingPayments = payments.Where(p => p.Status == PaymentStatus.Pending).Sum(p => p.Amount),
            ReleasedPayments = payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount),
            CampaignSpend = await db.Campaigns.Where(c => c.BrandProfileId == brand.Id).SumAsync(c => c.SpentAmount)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateNotificationPreferences(NotificationPreferencesFormViewModel model)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var userId = userManager.GetUserId(User)!;
        var prefs = await db.NotificationPreferences.FirstOrDefaultAsync(p => p.UserId == userId);

        if (prefs is null)
        {
            prefs = new NotificationPreference { UserId = userId };
            db.NotificationPreferences.Add(prefs);
        }

        prefs.NewProposals = model.NewProposals;
        prefs.ProposalUpdates = model.ProposalUpdates;
        prefs.Messages = model.Messages;
        prefs.MilestoneUpdates = model.MilestoneUpdates;
        prefs.PaymentUpdates = model.PaymentUpdates;
        prefs.CampaignDeadlines = model.CampaignDeadlines;

        await db.SaveChangesAsync();

        TempData["NotificationPrefsSaved"] = "true";
        return Redirect("/Settings#notifications");
    }

    public async Task<IActionResult> Profile()
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var user = await userManager.GetUserAsync(User);
        ViewData["Email"] = user?.Email;
        ViewData["CampaignCount"] = await db.Campaigns.CountAsync(c => c.BrandProfileId == brand.Id);

        return View(brand);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Update(BrandSettingsViewModel model)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        if (!ModelState.IsValid)
        {
            TempData["ProfileErrors"] = string.Join("|", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return RedirectToAction("Index");
        }

        if (model.RemoveProfilePicture && brand.ProfilePictureUrl is not null)
        {
            mediaUploads.DeleteMedia(brand.ProfilePictureUrl);
            brand.ProfilePictureUrl = null;
        }

        if (model.ProfilePictureFile is { Length: > 0 })
        {
            var (url, error) = await mediaUploads.SaveProfilePictureAsync(model.ProfilePictureFile);
            if (error is not null)
            {
                TempData["ProfileErrors"] = error;
                return RedirectToAction("Index");
            }

            mediaUploads.DeleteMedia(brand.ProfilePictureUrl);
            brand.ProfilePictureUrl = url;
        }

        brand.CompanyName = model.CompanyName;
        brand.ContactFullName = model.ContactFullName;
        brand.WebsiteUrl = model.WebsiteUrl;
        brand.Industry = model.Industry;
        brand.MonthlyBudget = model.MonthlyBudget;

        await db.SaveChangesAsync();

        TempData["ProfileSaved"] = "true";
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        if (!ModelState.IsValid)
        {
            TempData["PasswordErrors"] = string.Join("|", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return RedirectToAction("Index");
        }

        var user = await userManager.GetUserAsync(User);
        var result = await userManager.ChangePasswordAsync(user!, model.CurrentPassword, model.NewPassword);

        if (!result.Succeeded)
        {
            TempData["PasswordErrors"] = string.Join("|", result.Errors.Select(e => e.Description));
            return RedirectToAction("Index");
        }

        TempData["PasswordChanged"] = "true";
        return RedirectToAction("Index");
    }
}
