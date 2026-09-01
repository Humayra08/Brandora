using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Brandora.Web.Models;
using Brandora.Web.Models.Discovery;
using Brandora.Web.Models.Contact;

namespace Brandora.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("platform")]
    public IActionResult Platform()
    {
        return View();
    }

    [HttpGet("for-brands")]
    public IActionResult ForBrands()
    {
        return View(DirectoryData.BuildBrandDirectory());
    }

    [HttpGet("for-influencers")]
    public IActionResult ForInfluencers()
    {
        return View(DirectoryData.BuildInfluencerDirectory());
    }

    [HttpGet("contact")]
    public IActionResult Contact()
    {
        return View(new ContactIssueViewModel());
    }

    [HttpPost("contact")]
    [ValidateAntiForgeryToken]
    public IActionResult Contact(ContactIssueViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Reports are acknowledged in-page; support picks them up over the
        // channels listed alongside the form.
        TempData["ContactSubmitted"] = true;

        return RedirectToAction(nameof(Contact));
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
