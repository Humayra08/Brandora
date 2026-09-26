using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Brandora.Web.Models.Messages;
using Brandora.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

public class InfluencerMessagesController(UserManager<ApplicationUser> userManager, ApplicationDbContext db, MediaUploadService mediaUploads, NotificationService notifications) : InfluencerControllerBase(userManager, db)
{
    public async Task<IActionResult> Index(int? open, int? campaignId, string? search)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var query = db.Conversations.Where(c => c.InfluencerProfileId == influencer.Id);

        if (campaignId.HasValue)
        {
            query = query.Where(c => c.CampaignId == campaignId.Value);
        }

        var conversations = await query
            .Include(c => c.BrandProfile)
            .Include(c => c.Campaign)
            .Include(c => c.Messages)
            .ToListAsync();

        if (!string.IsNullOrWhiteSpace(search))
        {
            conversations = conversations.Where(c =>
                c.BrandProfile.CompanyName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (c.Campaign != null && c.Campaign.Title.Contains(search, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        // A thread this side deleted stays out of the inbox until something new is said
        // in it — unless it's the one being opened right now (e.g. "Message" again).
        conversations = conversations
            .Where(c => c.InfluencerClearedAt is null || c.VisibleMessages(false).Any() || c.Id == open)
            .ToList();

        var userId = userManager.GetUserId(User);
        var unreadCounts = conversations.ToDictionary(
            c => c.Id,
            c => c.VisibleMessages(false).Count(m => m.SenderUserId != userId && m.ReadAt == null));

        // Pinned threads first (most recently pinned on top), then by latest activity.
        var ordered = conversations
            .OrderByDescending(c => c.InfluencerPinnedAt.HasValue)
            .ThenByDescending(c => c.InfluencerPinnedAt)
            .ThenByDescending(c => c.VisibleMessages(false).Select(m => (DateTime?)m.SentAt).Max() ?? c.InfluencerClearedAt ?? c.CreatedAt)
            .ToList();

        var vm = new InboxViewModel
        {
            Conversations = ordered,
            UnreadCounts = unreadCounts,
            Search = search,
            TotalCount = ordered.Count,
            TotalUnread = unreadCounts.Values.Sum(),
            InfluencerName = influencer.FullName,
            Notifications = await db.Notifications.AsNoTracking()
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt).Take(5).ToListAsync()
        };

        var targetId = open ?? ordered.FirstOrDefault()?.Id;
        if (targetId.HasValue)
        {
            var selected = await db.Conversations
                .Include(c => c.BrandProfile)
                .Include(c => c.Campaign)
                .Include(c => c.Messages).ThenInclude(m => m.SenderUser)
                .FirstOrDefaultAsync(c => c.Id == targetId.Value && c.InfluencerProfileId == influencer.Id);

            if (selected is not null)
            {
                var unread = selected.VisibleMessages(false).Where(m => m.SenderUserId != userId && m.ReadAt == null).ToList();
                if (unread.Count > 0)
                {
                    foreach (var message in unread)
                    {
                        message.ReadAt = DateTime.UtcNow;
                    }

                    await db.SaveChangesAsync();

                    vm.UnreadCounts[selected.Id] = 0;
                    vm.TotalUnread = vm.UnreadCounts.Values.Sum();
                }

                vm.Selected = selected;
            }
        }

        return View(vm);
    }

    public IActionResult Conversation(int id)
    {
        return RedirectToAction("Index", new { open = id });
    }

    // Pin / unpin a thread to the top of THIS side's inbox only.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePin(int conversationId, int? open)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var conversation = await db.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId && c.InfluencerProfileId == influencer.Id);
        if (conversation is null)
        {
            return NotFound();
        }

        conversation.InfluencerPinnedAt = conversation.InfluencerPinnedAt is null ? DateTime.UtcNow : null;
        await db.SaveChangesAsync();

        return RedirectToAction("Index", new { open = open ?? conversationId });
    }

    // "Delete conversation" for THIS side only: hides the thread and its history from
    // this inbox. The other side keeps their full copy; nothing is removed from the
    // database. If a new message arrives later, the thread comes back showing only
    // what was said after the delete.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConversation(int conversationId, int? open)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var conversation = await db.Conversations
            .Include(c => c.BrandProfile)
            .FirstOrDefaultAsync(c => c.Id == conversationId && c.InfluencerProfileId == influencer.Id);
        if (conversation is null)
        {
            return NotFound();
        }

        conversation.InfluencerClearedAt = DateTime.UtcNow;
        conversation.InfluencerPinnedAt = null;
        await db.SaveChangesAsync();

        var otherName = conversation.BrandProfile.CompanyName;
        TempData["InboxNotice"] = $"Conversation with {otherName} deleted from your inbox. {otherName} still has their copy.";

        return open is not null && open != conversationId
            ? RedirectToAction("Index", new { open })
            : RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> Send(int conversationId, string? body, IFormFile? mediaFile)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var conversation = await db.Conversations
            .Include(c => c.BrandProfile)
            .FirstOrDefaultAsync(c => c.Id == conversationId && c.InfluencerProfileId == influencer.Id);
        if (conversation is null)
        {
            return NotFound();
        }

        string? mediaUrl = null;
        string? mediaType = null;

        if (mediaFile is { Length: > 0 })
        {
            var (url, type, error) = await mediaUploads.SaveMediaAsync(mediaFile, "messages");
            if (error is not null)
            {
                TempData["MessageError"] = error;
                return RedirectToAction("Index", new { open = conversationId });
            }

            mediaUrl = url;
            mediaType = type;
        }

        var trimmedBody = body?.Trim() ?? string.Empty;
        var hasBody = !string.IsNullOrEmpty(trimmedBody);
        var hasMedia = mediaUrl is not null;

        if (hasBody || hasMedia)
        {
            var senderId = userManager.GetUserId(User)!;

            // Sent as two separate messages when both an attachment and a
            // caption are submitted together, so the attachment always
            // renders in its own bubble rather than a mixed image+text one.
            if (hasMedia)
            {
                db.Messages.Add(new Message
                {
                    ConversationId = conversation.Id,
                    SenderUserId = senderId,
                    MediaUrl = mediaUrl,
                    MediaType = mediaType
                });
            }

            if (hasBody)
            {
                db.Messages.Add(new Message
                {
                    ConversationId = conversation.Id,
                    SenderUserId = senderId,
                    Body = trimmedBody
                });
            }

            await db.SaveChangesAsync();

            await notifications.NotifyAsync(
                conversation.BrandProfile.UserId,
                "Message",
                "New message",
                $"You have a new message from {influencer.FullName}.",
                $"/Messages?open={conversation.Id}");
            await db.SaveChangesAsync();
        }

        return RedirectToAction("Index", new { open = conversationId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditMessage(int messageId, int conversationId, string? body)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var userId = userManager.GetUserId(User);
        var message = await db.Messages
            .Include(m => m.Conversation)
            .FirstOrDefaultAsync(m => m.Id == messageId && m.Conversation.InfluencerProfileId == influencer.Id);

        if (message is not null && message.SenderUserId == userId && string.IsNullOrEmpty(message.MediaUrl))
        {
            var trimmed = body?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(trimmed))
            {
                message.Body = trimmed;
                await db.SaveChangesAsync();
            }
        }

        return RedirectToAction("Index", new { open = conversationId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMessage(int messageId, int conversationId)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var userId = userManager.GetUserId(User);
        var message = await db.Messages
            .Include(m => m.Conversation)
            .FirstOrDefaultAsync(m => m.Id == messageId && m.Conversation.InfluencerProfileId == influencer.Id);

        if (message is not null && message.SenderUserId == userId)
        {
            db.Messages.Remove(message);
            await db.SaveChangesAsync();
        }

        return RedirectToAction("Index", new { open = conversationId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartWithBrand(int brandId, int? campaignId)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var brandExists = await db.BrandProfiles.AnyAsync(b => b.Id == brandId);
        if (!brandExists)
        {
            return NotFound();
        }

        // One conversation per (brand, influencer) pair, regardless of which
        // campaign started it — CampaignId is stored only as first-contact
        // context, never used to fork a second thread.
        var conversation = await db.Conversations.OrderBy(c => c.Id).FirstOrDefaultAsync(c =>
            c.BrandProfileId == brandId && c.InfluencerProfileId == influencer.Id);

        if (conversation is null)
        {
            conversation = new Conversation
            {
                BrandProfileId = brandId,
                InfluencerProfileId = influencer.Id,
                CampaignId = campaignId
            };

            db.Conversations.Add(conversation);
            await db.SaveChangesAsync();
        }

        return RedirectToAction("Index", new { open = conversation.Id });
    }

    // ---- Pop-up chat window (opened from "Message" while browsing brands) ----
    // The exact counterpart of MessagesController's Widget / SendWidget /
    // EditMessageWidget / DeleteMessageWidget on the Brand side, rendering the same
    // _WidgetMessages partial so both sides' chat windows look and behave identically.

    public async Task<IActionResult> Widget(int brandId)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null)
        {
            return Unauthorized();
        }

        var brand = await db.BrandProfiles.FirstOrDefaultAsync(b => b.Id == brandId);
        if (brand is null)
        {
            return NotFound();
        }

        var conversation = await db.Conversations
            .Include(c => c.Messages).ThenInclude(m => m.SenderUser)
            .OrderBy(c => c.Id).FirstOrDefaultAsync(c => c.BrandProfileId == brandId && c.InfluencerProfileId == influencer.Id);

        if (conversation is null)
        {
            conversation = new Conversation { BrandProfileId = brandId, InfluencerProfileId = influencer.Id };
            db.Conversations.Add(conversation);
            await db.SaveChangesAsync();
        }

        var userId = userManager.GetUserId(User);
        var unread = conversation.VisibleMessages(false).Where(m => m.SenderUserId != userId && m.ReadAt == null).ToList();
        if (unread.Count > 0)
        {
            foreach (var message in unread)
            {
                message.ReadAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
        }

        ViewData["ConversationId"] = conversation.Id;
        ViewData["CreatorName"] = brand.CompanyName;
        ViewData["CreatorId"] = brand.Id;

        return PartialView("~/Views/Messages/_WidgetMessages.cshtml", conversation.VisibleMessages(false).OrderBy(m => m.SentAt).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> SendWidget(int brandId, string? body, IFormFile? mediaFile)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null)
        {
            return Unauthorized();
        }

        var conversation = await db.Conversations
            .Include(c => c.BrandProfile)
            .Include(c => c.Messages).ThenInclude(m => m.SenderUser)
            .OrderBy(c => c.Id).FirstOrDefaultAsync(c => c.BrandProfileId == brandId && c.InfluencerProfileId == influencer.Id);

        if (conversation is null)
        {
            var brandExists = await db.BrandProfiles.AnyAsync(b => b.Id == brandId);
            if (!brandExists)
            {
                return NotFound();
            }

            conversation = new Conversation { BrandProfileId = brandId, InfluencerProfileId = influencer.Id };
            db.Conversations.Add(conversation);
            await db.SaveChangesAsync();
            await db.Entry(conversation).Reference(c => c.BrandProfile).LoadAsync();
        }

        string? mediaUrl = null;
        string? mediaType = null;

        if (mediaFile is { Length: > 0 })
        {
            var (url, type, error) = await mediaUploads.SaveMediaAsync(mediaFile, "messages");
            if (error is not null)
            {
                return BadRequest(error);
            }

            mediaUrl = url;
            mediaType = type;
        }

        var trimmed = body?.Trim() ?? string.Empty;
        var hasBody = !string.IsNullOrEmpty(trimmed);
        var hasMedia = mediaUrl is not null;

        if (hasBody || hasMedia)
        {
            var senderId = userManager.GetUserId(User)!;

            if (hasMedia)
            {
                db.Messages.Add(new Message { ConversationId = conversation.Id, SenderUserId = senderId, MediaUrl = mediaUrl, MediaType = mediaType });
            }

            if (hasBody)
            {
                db.Messages.Add(new Message { ConversationId = conversation.Id, SenderUserId = senderId, Body = trimmed });
            }

            await db.SaveChangesAsync();

            await notifications.NotifyAsync(
                conversation.BrandProfile.UserId,
                "Message",
                "New message",
                $"You have a new message from {influencer.FullName}.",
                $"/Messages?open={conversation.Id}");
            await db.SaveChangesAsync();
        }

        ViewData["ConversationId"] = conversation.Id;
        ViewData["CreatorName"] = conversation.BrandProfile.CompanyName;
        ViewData["CreatorId"] = conversation.BrandProfileId;

        return PartialView("~/Views/Messages/_WidgetMessages.cshtml", conversation.VisibleMessages(false).OrderBy(m => m.SentAt).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditMessageWidget(int messageId, int brandId, string? body)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null)
        {
            return Unauthorized();
        }

        var userId = userManager.GetUserId(User);
        var message = await db.Messages
            .Include(m => m.Conversation)
            .FirstOrDefaultAsync(m => m.Id == messageId && m.Conversation.InfluencerProfileId == influencer.Id);

        if (message is not null && message.SenderUserId == userId && string.IsNullOrEmpty(message.MediaUrl))
        {
            var trimmed = body?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(trimmed))
            {
                message.Body = trimmed;
                await db.SaveChangesAsync();
            }
        }

        return await WidgetMessagesPartial(influencer.Id, brandId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMessageWidget(int messageId, int brandId)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null)
        {
            return Unauthorized();
        }

        var userId = userManager.GetUserId(User);
        var message = await db.Messages
            .Include(m => m.Conversation)
            .FirstOrDefaultAsync(m => m.Id == messageId && m.Conversation.InfluencerProfileId == influencer.Id);

        if (message is not null && message.SenderUserId == userId)
        {
            db.Messages.Remove(message);
            await db.SaveChangesAsync();
        }

        return await WidgetMessagesPartial(influencer.Id, brandId);
    }

    private async Task<IActionResult> WidgetMessagesPartial(int influencerId, int brandId)
    {
        var conversation = await db.Conversations
            .Include(c => c.BrandProfile)
            .Include(c => c.Messages).ThenInclude(m => m.SenderUser)
            .OrderBy(c => c.Id).FirstOrDefaultAsync(c => c.BrandProfileId == brandId && c.InfluencerProfileId == influencerId);

        if (conversation is null)
        {
            return NotFound();
        }

        ViewData["ConversationId"] = conversation.Id;
        ViewData["CreatorName"] = conversation.BrandProfile.CompanyName;
        ViewData["CreatorId"] = conversation.BrandProfileId;

        return PartialView("~/Views/Messages/_WidgetMessages.cshtml", conversation.VisibleMessages(false).OrderBy(m => m.SentAt).ToList());
    }
}
