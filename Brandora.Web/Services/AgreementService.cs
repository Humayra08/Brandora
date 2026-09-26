using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Services;

// Builds the frozen legal text of a Campaign Collaboration Agreement and activates the
// collaboration once both parties have signed. The ContentHtml built here is the
// immutable body of the document (parties, deliverables, legal terms) — it deliberately
// excludes the live signature blocks, which the Detail views render separately by
// joining today's Agreement.Signatures, so re-rendering an already-signed agreement
// never touches the hashed content.
public class AgreementService(ApplicationDbContext db)
{
    public async Task<Agreement> CreateAsync(Proposal proposal, Campaign campaign, BrandProfile brand, InfluencerProfile influencer)
    {
        var plans = await db.CampaignMilestonePlans.AsNoTracking()
            .Where(p => p.CampaignId == campaign.Id)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .ToListAsync();

        var content = BuildContentHtml(campaign, brand, influencer, plans);
        var code = $"BRA-{DateTime.UtcNow:yyyy}-{Guid.NewGuid().GetHashCode() & 0x7fffffff % 100000:D5}";

        var agreement = new Agreement
        {
            Code = code,
            Version = 1.0m,
            CampaignId = campaign.Id,
            ProposalId = proposal.Id,
            BrandProfileId = brand.Id,
            InfluencerProfileId = influencer.Id,
            Status = AgreementStatus.AwaitingBrandSignature,
            ContentHtml = content,
            ContentHash = ComputeHash(content)
        };

        db.Agreements.Add(agreement);
        agreement.Events.Add(new AgreementEvent { Agreement = agreement, EventType = "Generated", UserId = null, Detail = $"Generated from proposal #{proposal.Id}." });

        return agreement;
    }

    // Turns a canvas signature pad's `data:image/png;base64,...` export into an IFormFile
    // so a drawn signature can go through the exact same Cloudinary upload/validation path
    // (MediaUploadService.SaveMediaAsync) as an uploaded signature image.
    public static IFormFile? DataUrlToFormFile(string? dataUrl, string fileName)
    {
        if (string.IsNullOrWhiteSpace(dataUrl)) return null;

        var commaIndex = dataUrl.IndexOf(',');
        if (commaIndex < 0 || !dataUrl.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(dataUrl[(commaIndex + 1)..]);
        }
        catch (FormatException)
        {
            return null;
        }

        if (bytes.Length == 0) return null;

        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, stream.Length, "signature", fileName)
        {
            Headers = new Microsoft.AspNetCore.Http.HeaderDictionary(),
            ContentType = "image/png"
        };
    }

    public static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // Runs once both signatures exist: activates the collaboration exactly the way
    // ProposalsController.ConfirmAccept used to, immediately on accept — now gated
    // behind a fully-signed agreement instead.
    public async Task<Collaboration> ActivateCollaborationAsync(Agreement agreement)
    {
        var proposal = await db.Proposals.FirstAsync(p => p.Id == agreement.ProposalId);

        var collaboration = new Collaboration
        {
            ProposalId = proposal.Id,
            CampaignId = agreement.CampaignId,
            InfluencerProfileId = agreement.InfluencerProfileId,
            Status = CollaborationStatus.Active
        };
        db.Collaborations.Add(collaboration);

        // One conversation per brand/creator pair (the same rule Messages uses): if they
        // have already talked — about this campaign or any other — the collaboration
        // continues in that thread instead of opening a second one.
        var conversationExists = await db.Conversations.AnyAsync(c =>
            c.BrandProfileId == agreement.BrandProfileId && c.InfluencerProfileId == agreement.InfluencerProfileId);
        if (!conversationExists)
        {
            db.Conversations.Add(new Conversation
            {
                BrandProfileId = agreement.BrandProfileId,
                InfluencerProfileId = agreement.InfluencerProfileId,
                CampaignId = agreement.CampaignId
            });
        }

        var plans = await db.CampaignMilestonePlans.AsNoTracking()
            .Where(p => p.CampaignId == agreement.CampaignId)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .ToListAsync();

        foreach (var plan in plans)
        {
            db.Milestones.Add(new Milestone
            {
                Collaboration = collaboration,
                Title = plan.Title,
                ContentType = plan.ContentType,
                Description = plan.Description,
                Amount = plan.Amount,
                DueDate = plan.DueDate,
                Status = MilestoneStatus.Pending
            });
        }

        return collaboration;
    }

    private static string BuildContentHtml(Campaign campaign, BrandProfile brand, InfluencerProfile influencer, List<CampaignMilestonePlan> plans)
    {
        string Money(decimal amount) => "৳" + amount.ToString("N0", CultureInfo.InvariantCulture);
        string DateText(DateTime? date) => date?.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture) ?? "Not specified";
        var total = plans.Sum(p => p.Amount);

        var sb = new StringBuilder();
        sb.Append("<section class=\"agreement-doc\">");

        sb.Append("<h1>Campaign Collaboration Agreement</h1>");

        sb.Append("<h2>1. Parties</h2>");
        sb.Append($"<p><strong>Brand:</strong> {brand.CompanyName}");
        if (!string.IsNullOrWhiteSpace(brand.WebsiteUrl)) sb.Append($" ({brand.WebsiteUrl})");
        sb.Append("</p>");
        sb.Append($"<p><strong>Influencer:</strong> {influencer.FullName}");
        if (!string.IsNullOrWhiteSpace(influencer.PlatformUsername)) sb.Append($" (@{influencer.PlatformUsername})");
        sb.Append("</p>");

        sb.Append("<h2>2. Campaign Details</h2>");
        sb.Append("<table class=\"agreement-kv\">");
        sb.Append($"<tr><th>Campaign Name</th><td>{campaign.Title}</td></tr>");
        sb.Append($"<tr><th>Campaign ID</th><td>CAM-{campaign.CreatedAt.Year}-{campaign.Id:D3}</td></tr>");
        sb.Append($"<tr><th>Start Date</th><td>{DateText(campaign.StartDate)}</td></tr>");
        sb.Append($"<tr><th>End Date</th><td>{DateText(campaign.Deadline)}</td></tr>");
        sb.Append($"<tr><th>Platform(s)</th><td>{(string.IsNullOrWhiteSpace(campaign.Platform) ? "Not specified" : campaign.Platform)}</td></tr>");
        sb.Append($"<tr><th>Total Campaign Value</th><td>{Money(total)}</td></tr>");
        sb.Append("</table>");

        sb.Append("<h2>3. Scope of Work &amp; Deliverables</h2>");
        sb.Append("<table class=\"agreement-table\"><thead><tr><th>Deliverable</th><th>Requirements</th><th>Payment</th></tr></thead><tbody>");
        foreach (var plan in plans)
        {
            var requirements = string.IsNullOrWhiteSpace(plan.Description) ? (plan.ContentType ?? "As agreed") : plan.Description;
            sb.Append($"<tr><td>{plan.Title}</td><td>{requirements}</td><td>{Money(plan.Amount)}</td></tr>");
        }
        sb.Append($"<tr class=\"agreement-total-row\"><td colspan=\"2\">Total</td><td>{Money(total)}</td></tr>");
        sb.Append("</tbody></table>");

        sb.Append("<h2>4. Payment Terms</h2>");
        sb.Append("<p>Payment for each deliverable is released once the corresponding milestone's proof of posting has been reviewed and approved. Funds are transferred via Brandora's supported payment methods (bKash/Nagad) once approved. Brandora's own platform service fee is governed separately under Brandora's Terms of Service and is not part of this agreement.</p>");

        sb.Append("<h2>5. Content &amp; Revision Requirements</h2>");
        sb.Append($"<p>{(string.IsNullOrWhiteSpace(campaign.ContentGuidelines) ? "Content must reasonably reflect the brand's guidance as communicated during the collaboration. The Influencer agrees to make good-faith revisions if requested, consistent with the deliverable requirements listed above." : campaign.ContentGuidelines)}</p>");

        sb.Append("<h2>6. Content Ownership &amp; Licensing</h2>");
        sb.Append("<p>The Influencer retains ownership of the content they create. By submitting a deliverable, the Influencer grants the Brand a non-exclusive, royalty-free license to use, repost, and reference the approved content for promotional purposes related to this campaign, unless otherwise agreed in writing between the parties.</p>");

        sb.Append("<h2>7. Disclosure Requirements</h2>");
        sb.Append("<p>The Influencer agrees to clearly disclose this paid partnership in accordance with applicable advertising standards and platform policies (e.g. using \"#ad\", \"#sponsored\", or the platform's paid-partnership tools) in every deliverable under this agreement.</p>");

        sb.Append("<h2>8. Deadlines</h2>");
        sb.Append("<p>Each deliverable is due by the date specified for its milestone. Extensions may be agreed between the parties in writing (including via Brandora's messaging).</p>");

        sb.Append("<h2>9. Termination &amp; Cancellation</h2>");
        sb.Append("<p>Either party may raise concerns about the collaboration through Brandora's dispute process. Milestones already approved and paid are not affected by a later termination. Unpaid, incomplete deliverables may be cancelled by mutual agreement or resolved through Brandora's dispute resolution process.</p>");

        sb.Append("<h2>10. Dispute Provisions</h2>");
        sb.Append("<p>Any disagreement regarding deliverables, approvals, or payments under this agreement should first be raised through Brandora's in-app dispute process, which reviews the milestone proof, campaign requirements, and this agreement before a decision is made.</p>");

        sb.Append("<h2>11. Signature &amp; Acceptance</h2>");
        sb.Append("<p>By signing below, both parties confirm they have read, understood, and agree to be bound by the terms of this Campaign Collaboration Agreement.</p>");

        sb.Append("</section>");
        return sb.ToString();
    }
}
