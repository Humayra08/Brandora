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
}
