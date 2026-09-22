using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Brandora.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

// Brand-facing side of the Campaign Collaboration Agreement e-signature flow.
public class AgreementsController(UserManager<ApplicationUser> userManager, ApplicationDbContext db, AgreementService agreements, MediaUploadService mediaUploads, NotificationService notifications)
    : BrandControllerBase(userManager, db)
{
    public async Task<IActionResult> Index(string? tab)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null) return RedirectToAction("Index", "Home");

        var list = await db.Agreements.AsNoTracking()
            .Include(a => a.Campaign)
            .Include(a => a.InfluencerProfile)
            .Where(a => a.BrandProfileId == brand.Id)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        var filtered = tab switch
        {
            "mine" => list.Where(a => a.Status == AgreementStatus.AwaitingBrandSignature).ToList(),
            "them" => list.Where(a => a.Status == AgreementStatus.AwaitingInfluencerSignature).ToList(),
            "completed" => list.Where(a => a.Status == AgreementStatus.FullySigned).ToList(),
            _ => list
        };

        ViewData["Tab"] = tab ?? "all";
        return View(filtered);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int proposalId)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null) return RedirectToAction("Index", "Home");

        var proposal = await db.Proposals
            .Include(p => p.Campaign)
            .Include(p => p.InfluencerProfile)
            .FirstOrDefaultAsync(p => p.Id == proposalId && p.Campaign.BrandProfileId == brand.Id);

        if (proposal is null) return NotFound();
        if (proposal.Status != ProposalStatus.Accepted) return RedirectToAction("Detail", "Proposals", new { id = proposalId });

        var existing = await db.Agreements.FirstOrDefaultAsync(a => a.ProposalId == proposalId);
        if (existing is not null) return RedirectToAction(nameof(Detail), new { id = existing.Id });

        var agreement = await agreements.CreateAsync(proposal, proposal.Campaign, brand, proposal.InfluencerProfile);
        await db.SaveChangesAsync();

        return RedirectToAction(nameof(Detail), new { id = agreement.Id });
    }

    public async Task<IActionResult> Detail(int id)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null) return RedirectToAction("Index", "Home");

        var agreement = await db.Agreements
            .Include(a => a.Campaign)
            .Include(a => a.BrandProfile)
            .Include(a => a.InfluencerProfile)
            .Include(a => a.Signatures)
            .FirstOrDefaultAsync(a => a.Id == id && a.BrandProfileId == brand.Id);

        if (agreement is null) return NotFound();

        var userId = userManager.GetUserId(User);
        var alreadyViewed = await db.AgreementEvents.AnyAsync(e => e.AgreementId == id && e.EventType == "BrandViewed");
        if (!alreadyViewed)
        {
            db.AgreementEvents.Add(new AgreementEvent { AgreementId = id, EventType = "BrandViewed", UserId = userId });
            await db.SaveChangesAsync();
        }

        return View(agreement);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Sign(int id, string signerName, bool consent, string? signatureDataUrl, IFormFile? signatureFile, string? signatureMode)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null) return RedirectToAction("Index", "Home");

        var agreement = await db.Agreements
            .Include(a => a.Campaign)
            .Include(a => a.InfluencerProfile)
            .FirstOrDefaultAsync(a => a.Id == id && a.BrandProfileId == brand.Id);

        if (agreement is null) return NotFound();
        if (agreement.Status != AgreementStatus.AwaitingBrandSignature) return RedirectToAction(nameof(Detail), new { id });

        if (!consent || string.IsNullOrWhiteSpace(signerName) || signerName.Trim().Length > 150)
        {
            TempData["AgreementError"] = "Enter your name and confirm you agree to the terms before signing.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        if (signatureMode is not (null or "draw" or "type" or "upload"))
        {
            TempData["AgreementError"] = "Choose a valid signature method.";
            return RedirectToAction(nameof(Detail), new { id });
        }
        if (signatureMode is "draw" or "type") signatureFile = null;
        if (signatureMode == "upload") signatureDataUrl = null;
        var (signatureUrl, method, error) = await UploadSignatureAsync(signatureDataUrl, signatureFile);
        if (signatureMode == "type" && error is null) method = SignatureMethod.Typed;
        if (error is not null)
        {
            TempData["AgreementError"] = error;
            return RedirectToAction(nameof(Detail), new { id });
        }

        var userId = userManager.GetUserId(User)!;
        db.AgreementSignatures.Add(new AgreementSignature
        {
            AgreementId = id,
            Party = AgreementParty.Brand,
            SignerUserId = userId,
            SignerName = signerName.Trim(),
            SignatureImageUrl = signatureUrl!,
            Method = method,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });
        agreement.Status = AgreementStatus.AwaitingInfluencerSignature;
        db.AgreementEvents.Add(new AgreementEvent { AgreementId = id, EventType = "BrandSigned", UserId = userId });
        await db.SaveChangesAsync();

        await notifications.NotifyAsync(
            agreement.InfluencerProfile.UserId,
            "Collaboration",
            "Agreement ready for signature",
            $"Your application for \"{agreement.Campaign.Title}\" has been accepted and the brand has signed the campaign agreement.",
            $"/InfluencerAgreements/Detail/{agreement.Id}");
        await db.SaveChangesAsync();

        return RedirectToAction(nameof(Detail), new { id });
    }

    internal async Task<(string? Url, SignatureMethod Method, string? Error)> UploadSignatureAsync(string? signatureDataUrl, IFormFile? signatureFile)
    {
        if (signatureFile is { Length: > 0 })
        {
            if (signatureFile.Length > 5 * 1024 * 1024 || signatureFile.ContentType is not ("image/png" or "image/jpeg" or "image/webp"))
                return (null, SignatureMethod.Uploaded, "Choose a PNG, JPG or WebP signature image smaller than 5 MB.");
            var (url, _, error) = await mediaUploads.SaveMediaAsync(signatureFile, "signatures");
            return error is not null ? (null, SignatureMethod.Uploaded, error) : (url, SignatureMethod.Uploaded, null);
        }

        var drawnFile = AgreementService.DataUrlToFormFile(signatureDataUrl, "signature.png");
        if (drawnFile is not null)
        {
            var (url, _, error) = await mediaUploads.SaveMediaAsync(drawnFile, "signatures");
            return error is not null ? (null, SignatureMethod.Drawn, error) : (url, SignatureMethod.Drawn, null);
        }

        return (null, SignatureMethod.Drawn, "Draw your signature or upload a signature image before signing.");
    }
}
