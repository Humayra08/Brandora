using Brandora.Web.Data;
using Brandora.Web.Models.Account;
using Brandora.Web.Models.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Brandora.Web.Controllers;

public class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ApplicationDbContext db) : Controller
{
    public IActionResult Register(string? role)
    {
        var initialView = string.Equals(role, "Brand", StringComparison.OrdinalIgnoreCase)
            ? "brand"
            : string.Equals(role, "Influencer", StringComparison.OrdinalIgnoreCase)
                ? "influencer"
                : "select";

        return View(new RegisterPageViewModel { InitialView = initialView });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterBrand(BrandRegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("Register", new RegisterPageViewModel { InitialView = "brand", Brand = model });
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            DisplayName = model.CompanyName,
            Role = UserRole.Brand
        };

        var result = await userManager.CreateAsync(user, model.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View("Register", new RegisterPageViewModel { InitialView = "brand", Brand = model });
        }

        db.BrandProfiles.Add(new BrandProfile
        {
            UserId = user.Id,
            CompanyName = model.CompanyName,
            ContactFullName = model.ContactFullName,
            WebsiteUrl = model.WebsiteUrl,
            Industry = model.Industry,
            MonthlyBudget = model.MonthlyBudget
        });
        await db.SaveChangesAsync();

        await signInManager.SignInAsync(user, isPersistent: false);

        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterInfluencer(InfluencerRegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("Register", new RegisterPageViewModel { InitialView = "influencer", Influencer = model });
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            DisplayName = model.FullName,
            Role = UserRole.Influencer
        };

        var result = await userManager.CreateAsync(user, model.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View("Register", new RegisterPageViewModel { InitialView = "influencer", Influencer = model });
        }

        db.InfluencerProfiles.Add(new InfluencerProfile
        {
            UserId = user.Id,
            FullName = model.FullName,
            PrimaryPlatform = model.PrimaryPlatform,
            PlatformUsername = model.PlatformUsername,
            ContentNiche = model.ContentNiche,
            AudienceSize = model.AudienceSize,
            Followers = model.Followers,
            EngagementRate = model.EngagementRate,
            Location = model.Location,
            Bio = model.Bio,
            RateNote = model.RateNote
        });
        await db.SaveChangesAsync();

        await signInManager.SignInAsync(user, isPersistent: false);

        return RedirectToAction("Index", "Home");
    }

    public IActionResult Login()
    {
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email);

        if (user is not null)
        {
            var result = await signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                return user.Role == UserRole.Brand
                    ? RedirectToAction("Index", "Dashboard")
                    : RedirectToAction("Index", "Home");
            }
        }

        ModelState.AddModelError(string.Empty, "Invalid email or password.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    public IActionResult ForgotPassword()
    {
        return View();
    }
}
