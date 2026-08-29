using Brandora.Web.Data;
using Brandora.Web.Models.Domain;

namespace Brandora.Web.Services;

public class NotificationService(ApplicationDbContext db)
{
    public void Notify(string userId, string category, string title, string body, string? linkUrl = null)
    {
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
