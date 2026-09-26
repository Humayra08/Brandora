using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Brandora.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

// Influencer-facing side of the Campaign Collaboration Agreement e-signature flow.
public class InfluencerAgreementsController(UserManager<ApplicationUser> userManager, ApplicationDbContext db, AgreementService agreementService, MediaUploadService mediaUploads, NotificationService notifications)
    : InfluencerControllerBase(userManager, db)
{
    public async Task<IActionResult> Index(string? tab)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null) return RedirectToAction("Index", "Home");

        var list = await db.Agreements.AsNoTracking()
            .Include(a => a.Campaign)
            .Include(a => a.BrandProfile)
            .Where(a => a.InfluencerProfileId == influencer.Id)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        var filtered = tab switch
        {
            "mine" => list.Where(a => a.Status == AgreementStatus.AwaitingInfluencerSignature).ToList(),
            "them" => list.Where(a => a.Status == AgreementStatus.AwaitingBrandSignature).ToList(),
            "completed" => list.Where(a => a.Status == AgreementStatus.FullySigned).ToList(),
            _ => list
        };

        ViewData["Tab"] = tab is "mine" or "them" or "completed" ? tab : "all";
        ViewData["Counts"] = new Dictionary<string, int>
        {
            ["all"] = list.Count,
            ["mine"] = list.Count(a => a.Status == AgreementStatus.AwaitingInfluencerSignature),
            ["them"] = list.Count(a => a.Status == AgreementStatus.AwaitingBrandSignature),
            ["completed"] = list.Count(a => a.Status == AgreementStatus.FullySigned)
        };
        return View(filtered);
    }

    public async Task<IActionResult> Detail(int id)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null) return RedirectToAction("Index", "Home");

        var agreement = await db.Agreements
            .Include(a => a.Campaign)
            .Include(a => a.BrandProfile)
            .Include(a => a.InfluencerProfile)
            .Include(a => a.Signatures)
            .FirstOrDefaultAsync(a => a.Id == id && a.InfluencerProfileId == influencer.Id);

        if (agreement is null) return NotFound();

        var userId = userManager.GetUserId(User);
        var alreadyViewed = await db.AgreementEvents.AnyAsync(e => e.AgreementId == id && e.EventType == "InfluencerViewed");
        if (!alreadyViewed)
        {
            db.AgreementEvents.Add(new AgreementEvent { AgreementId = id, EventType = "InfluencerViewed", UserId = userId });
            await db.SaveChangesAsync();
        }

        return View(agreement);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Sign(int id, string signerName, bool consent, string? signatureDataUrl, IFormFile? signatureFile, string? signatureMode)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null) return RedirectToAction("Index", "Home");

        var agreement = await db.Agreements
            .Include(a => a.Campaign)
            .Include(a => a.BrandProfile)
            .FirstOrDefaultAsync(a => a.Id == id && a.InfluencerProfileId == influencer.Id);

        if (agreement is null) return NotFound();
        if (agreement.Status != AgreementStatus.AwaitingInfluencerSignature) return RedirectToAction(nameof(Detail), new { id });

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
            Party = AgreementParty.Influencer,
            SignerUserId = userId,
            SignerName = signerName.Trim(),
            SignatureImageUrl = signatureUrl!,
            Method = method,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        // Both signatures now exist — finalize and activate the collaboration.
        agreement.Status = AgreementStatus.FullySigned;
        db.AgreementEvents.Add(new AgreementEvent { AgreementId = id, EventType = "InfluencerSigned", UserId = userId });

        var collaboration = await agreementService.ActivateCollaborationAsync(agreement);
        db.AgreementEvents.Add(new AgreementEvent { AgreementId = id, EventType = "Finalized", UserId = userId, Detail = $"Collaboration #{collaboration.Id} activated." });

        await db.SaveChangesAsync();

        await notifications.NotifyAsync(
            influencer.UserId,
            "Collaboration",
            "Collaboration started",
            $"The agreement for \"{agreement.Campaign.Title}\" is fully signed — the collaboration is now active.",
            $"/InfluencerCampaigns/Details/{agreement.CampaignId}");

        var brandUserId = await db.Users.Where(u => u.BrandProfile != null && u.BrandProfile.Id == agreement.BrandProfileId).Select(u => u.Id).FirstOrDefaultAsync();
        if (brandUserId is not null)
        {
            await notifications.NotifyAsync(
                brandUserId,
                "Collaboration",
                "Collaboration started",
                $"Both sides have signed the agreement for \"{agreement.Campaign.Title}\" — the collaboration is now active.",
                $"/Collaborations/Detail/{collaboration.Id}");
        }
        await db.SaveChangesAsync();

        return RedirectToAction(nameof(Detail), new { id });
    }

    private async Task<(string? Url, SignatureMethod Method, string? Error)> UploadSignatureAsync(string? signatureDataUrl, IFormFile? signatureFile)
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
