using System.Security.Claims;
using Brandora.Web.Areas.Admin.Models;
using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Brandora.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Brandora.Web.Areas.Admin.Controllers;

[Area("Admin")]
public class AdminAccountController(
    AdminAuthService adminAuthService,
    IWebHostEnvironment env,
    UserManager<ApplicationUser> userManager) : Controller
{
    public IActionResult Login()
    {
        return View(new AdminLoginViewModel());
    }

    // Development-only helper: generates the PBKDF2 hash for a chosen admin password so you can
    // paste it into .env as ADMIN_n_PASSWORD. Never available outside Development — the plaintext
    // password never leaves your own browser/machine, and nothing here is logged or persisted.
    [HttpGet]
    public IActionResult HashPassword(string? password)
    {
        if (!env.IsDevelopment())
        {
            return NotFound();
        }

        ViewData["Hash"] = string.IsNullOrEmpty(password) ? null : AdminAuthService.HashPassword(password);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(AdminLoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var account = adminAuthService.Validate(model.Email, model.Password);

        if (account is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, account.Name),
            new(ClaimTypes.Email, account.Email),
            new("LastLoginUtc", DateTime.UtcNow.ToString("O"))
        };

        var identity = new ClaimsIdentity(claims, "AdminScheme");
        await HttpContext.SignInAsync("AdminScheme", new ClaimsPrincipal(identity));

        return RedirectToAction("Index", "AdminDashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("AdminScheme");
        return RedirectToAction("Login");
    }

    // A bare GET here (e.g. the browser reloading /Logout after the POST already signed
    // the admin out) should never show a raw 400 — just land back on the login page.
    [HttpGet]
    [ActionName("Logout")]
    public async Task<IActionResult> LogoutGet()
    {
        await HttpContext.SignOutAsync("AdminScheme");
        return RedirectToAction("Login");
    }

    // Development-only helper: deletes ONE test account by exact email — the ApplicationUser
    // and its Brand/Influencer profile cascade-delete together. Never available outside
    // Development, requires typing the exact email as confirmation, and only ever removes
    // the single account requested — never a bulk/wildcard delete.
    [HttpGet]
    public async Task<IActionResult> DeleteTestAccount(string? email, string? confirm)
    {
        if (!env.IsDevelopment())
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return View();
        }

        if (!string.Equals(email, confirm, StringComparison.OrdinalIgnoreCase))
        {
            ViewData["Error"] = "Type the email again in the confirm box exactly as above.";
            ViewData["Email"] = email;
            return View();
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            ViewData["Error"] = $"No account found for {email}.";
            return View();
        }

        var result = await userManager.DeleteAsync(user);
        ViewData["Result"] = result.Succeeded
            ? $"Deleted account and profile for {email}."
            : $"Failed: {string.Join(", ", result.Errors.Select(e => e.Description))}";

        return View();
    }
}
