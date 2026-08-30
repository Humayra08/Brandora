using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Brandora.Web.Models;
using Brandora.Web.Models.Discovery;

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
