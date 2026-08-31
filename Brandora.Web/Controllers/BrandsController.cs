using Brandora.Web.Data;
using Brandora.Web.Models.Brands;
using Brandora.Web.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

public class BrandsController(UserManager<ApplicationUser> userManager, ApplicationDbContext db) : InfluencerControllerBase(userManager, db)
{
    public async Task<IActionResult> Index(string? search, string? industry)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var query = db.BrandProfiles.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(b => b.CompanyName.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(industry))
        {
            query = query.Where(b => b.Industry == industry);
        }

        var brands = await query.OrderByDescending(b => b.CreatedAt).ToListAsync();

        var vm = new BrandListViewModel
        {
            Brands = brands,
            Search = search,
            Industry = industry,
            TotalCount = await db.BrandProfiles.CountAsync()
        };

        return View(vm);
    }
}
