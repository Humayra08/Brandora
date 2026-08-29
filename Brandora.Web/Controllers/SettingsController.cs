using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Brandora.Web.Models.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Brandora.Web.Controllers;

public class SettingsController(UserManager<ApplicationUser> userManager, ApplicationDbContext db) : BrandControllerBase(userManager, db)
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

        return View(new BrandSettingsViewModel
        {
            CompanyName = brand.CompanyName,
            ContactFullName = brand.ContactFullName,
            WebsiteUrl = brand.WebsiteUrl,
            Industry = brand.Industry,
            MonthlyBudget = brand.MonthlyBudget
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
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
