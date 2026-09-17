using Brandora.Web.Models.Domain;
using Microsoft.EntityFrameworkCore;
using Brandora.Web.Data;

namespace Brandora.Web.Services;

public class NotificationService(ApplicationDbContext db)
{
    // Real events created by this app all use one of these category strings.
    // Kept in sync with every notifications.Notify(...) call site.
    private static bool IsAllowed(NotificationPreference prefs, string category) => category switch
    {
        "Proposal" => prefs.NewProposals || prefs.ProposalUpdates,
        "Collaboration" => prefs.ProposalUpdates,
        "Milestone" => prefs.MilestoneUpdates,
        "Payment" => prefs.PaymentUpdates,
        "Message" => prefs.Messages,
        "Deadline" => prefs.CampaignDeadlines,
        _ => true
    };

    public async Task NotifyAsync(string userId, string category, string title, string body, string? linkUrl = null)
    {
        var prefs = await db.NotificationPreferences.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);

        // No row yet means the user has never opened Settings — defaults are
        // all-enabled, matching the NotificationPreference model's defaults.
        if (prefs is not null && !IsAllowed(prefs, category))
        {
            return;
        }

        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Category = category,
            Title = title,
            Body = body,
            LinkUrl = linkUrl
        });
    }

    // No background job scheduler exists in this app, so deadline reminders
    // are checked lazily on real page loads (Dashboard, Notifications) rather
    // than on a timer. Idempotent: a reminder is only created once per
    // campaign by checking for an existing "Deadline" notification linking to
    // that campaign before adding another.
    public async Task CheckCampaignDeadlinesAsync(int brandProfileId, string userId)
    {
        var now = DateTime.UtcNow;
        var horizon = now.AddDays(3);

        var upcoming = await db.Campaigns
            .Where(c => c.BrandProfileId == brandProfileId
                        && (c.Status == CampaignStatus.Published || c.Status == CampaignStatus.Active)
                        && c.Deadline.HasValue && c.Deadline.Value >= now && c.Deadline.Value <= horizon)
            .ToListAsync();

        if (upcoming.Count == 0)
        {
            return;
        }

        var existingLinks = await db.Notifications
            .Where(n => n.UserId == userId && n.Category == "Deadline")
            .Select(n => n.LinkUrl)
            .ToListAsync();

        foreach (var campaign in upcoming)
        {
            var linkUrl = $"/Campaigns/Detail/{campaign.Id}";
            if (existingLinks.Contains(linkUrl))
            {
                continue;
            }

            var daysLeft = (int)Math.Ceiling((campaign.Deadline!.Value - now).TotalDays);
            await NotifyAsync(
                userId,
                "Deadline",
                "Campaign deadline approaching",
                $"\"{campaign.Title}\" is due in {daysLeft} day{(daysLeft == 1 ? "" : "s")}.",
                linkUrl);
        }

        await db.SaveChangesAsync();
    }
}
