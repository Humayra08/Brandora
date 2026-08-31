using System.Security.Claims;
using Brandora.Web.Areas.Admin.Models;
using Brandora.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace Brandora.Web.Areas.Admin.Controllers;

[Area("Admin")]
public class AdminAccountController(AdminAuthService adminAuthService) : Controller
{
    public IActionResult Login()
    {
        return View(new AdminLoginViewModel());
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
}
