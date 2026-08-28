using Microsoft.AspNetCore.Mvc;

namespace Brandora.Web.Controllers;

public class AccountController : Controller
{
    public IActionResult Register(string? role)
    {
        var initialView = string.Equals(role, "Brand", StringComparison.OrdinalIgnoreCase)
            ? "brand"
            : string.Equals(role, "Influencer", StringComparison.OrdinalIgnoreCase)
                ? "influencer"
                : "select";

        ViewData["InitialView"] = initialView;

        return View();
    }

    public IActionResult Login()
    {
        return View();
    }

    public IActionResult ForgotPassword()
    {
        return View();
    }
}
