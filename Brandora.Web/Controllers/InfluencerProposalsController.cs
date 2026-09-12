using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Brandora.Web.Models.Proposals;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Brandora.Web.Controllers;

public class InfluencerProposalsController(UserManager<ApplicationUser> userManager, ApplicationDbContext db)
    : InfluencerControllerBase(userManager, db)
{
    public async Task<IActionResult> Index(string? search, ProposalStatus? status, string? sort, int page = 1)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null) return RedirectToAction("Index", "Home");
        if (status.HasValue && !Enum.IsDefined(status.Value)) status = null;
        search = search?.Trim();
        var query = db.Proposals.AsNoTracking().Where(p => p.InfluencerProfileId == influencer.Id);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Campaign.Title.Contains(search) || p.Campaign.BrandProfile.CompanyName.Contains(search));
        if (status.HasValue) query = query.Where(p => p.Status == status.Value);
        var vm = new InfluencerProposalsViewModel
        {
            InfluencerName = influencer.FullName, Search = search, Status = status,
            Sort = sort == "oldest" ? "oldest" : "newest", TotalCount = await query.CountAsync(),
            Notifications = await db.Notifications.AsNoTracking().Where(n => n.UserId == influencer.UserId)
                .OrderByDescending(n => n.CreatedAt).Take(5).ToListAsync()
        };
        vm.Page = Math.Clamp(page, 1, vm.TotalPages);
        var ordered = vm.Sort == "oldest"
            ? query.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id)
            : query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id);
        vm.Proposals = await ordered.Include(p => p.Campaign).ThenInclude(c => c.BrandProfile)
            .Skip((vm.Page - 1) * vm.PageSize).Take(vm.PageSize).ToListAsync();
        return View(vm);
    }
    public async Task<IActionResult> Detail(int id)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null) return RedirectToAction("Index", "Home");
        var proposal = await db.Proposals.AsNoTracking()
            .Include(p => p.Campaign).ThenInclude(c => c.BrandProfile).Include(p => p.Collaboration)
            .FirstOrDefaultAsync(p => p.Id == id && p.InfluencerProfileId == influencer.Id);
        return proposal is null ? NotFound() : View(proposal);
    }
}