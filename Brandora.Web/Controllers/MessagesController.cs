using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Brandora.Web.Models.Messages;
using Brandora.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

public class MessagesController(UserManager<ApplicationUser> userManager, ApplicationDbContext db, MediaUploadService mediaUploads, NotificationService notifications) : BrandControllerBase(userManager, db)
{
    public async Task<IActionResult> Index(int? open, int? campaignId, string? search)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var query = db.Conversations.Where(c => c.BrandProfileId == brand.Id);

        if (campaignId.HasValue)
        {
            query = query.Where(c => c.CampaignId == campaignId.Value);
        }

        var conversations = await query
            .Include(c => c.InfluencerProfile)
            .Include(c => c.Campaign)
            .Include(c => c.Messages)
            .ToListAsync();

        if (!string.IsNullOrWhiteSpace(search))
        {
            conversations = conversations.Where(c =>
                c.InfluencerProfile.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (c.Campaign != null && c.Campaign.Title.Contains(search, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        var userId = userManager.GetUserId(User);
        var unreadCounts = conversations.ToDictionary(
            c => c.Id,
            c => c.Messages.Count(m => m.SenderUserId != userId && m.ReadAt == null));

        var ordered = conversations
            .OrderByDescending(c => c.Messages.Count > 0 ? c.Messages.Max(m => m.SentAt) : c.CreatedAt)
            .ToList();

        var vm = new InboxViewModel
        {
            Conversations = ordered,
            UnreadCounts = unreadCounts,
            Search = search,
            TotalCount = ordered.Count,
            TotalUnread = unreadCounts.Values.Sum()
        };

        var targetId = open ?? ordered.FirstOrDefault()?.Id;
        if (targetId.HasValue)
        {
            var selected = await db.Conversations
                .Include(c => c.InfluencerProfile)
                .Include(c => c.Campaign)
                .Include(c => c.Messages).ThenInclude(m => m.SenderUser)
                .FirstOrDefaultAsync(c => c.Id == targetId.Value && c.BrandProfileId == brand.Id);

            if (selected is not null)
            {
                var unread = selected.Messages.Where(m => m.SenderUserId != userId && m.ReadAt == null).ToList();
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> Send(int conversationId, string? body, IFormFile? mediaFile)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var conversation = await db.Conversations
            .Include(c => c.InfluencerProfile)
            .FirstOrDefaultAsync(c => c.Id == conversationId && c.BrandProfileId == brand.Id);
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
                conversation.InfluencerProfile.UserId,
                "Message",
                "New message",
                $"You have a new message from {brand.CompanyName}.",
                $"/InfluencerMessages?open={conversation.Id}");
            await db.SaveChangesAsync();
        }

        return RedirectToAction("Index", new { open = conversationId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditMessage(int messageId, int conversationId, string? body)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var userId = userManager.GetUserId(User);
        var message = await db.Messages
            .Include(m => m.Conversation)
            .FirstOrDefaultAsync(m => m.Id == messageId && m.Conversation.BrandProfileId == brand.Id);

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
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var userId = userManager.GetUserId(User);
        var message = await db.Messages
            .Include(m => m.Conversation)
            .FirstOrDefaultAsync(m => m.Id == messageId && m.Conversation.BrandProfileId == brand.Id);

        if (message is not null && message.SenderUserId == userId)
        {
            db.Messages.Remove(message);
            await db.SaveChangesAsync();
        }

        return RedirectToAction("Index", new { open = conversationId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartWithCreator(int influencerId, int? campaignId)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var creatorExists = await db.InfluencerProfiles.AnyAsync(i => i.Id == influencerId);
        if (!creatorExists)
        {
            return NotFound();
        }

        // One conversation per (brand, influencer) pair, regardless of which
        // campaign the brand messages them about — CampaignId is stored only
        // as first-contact context, never used to fork a second thread.
        var conversation = await db.Conversations.FirstOrDefaultAsync(c =>
            c.BrandProfileId == brand.Id && c.InfluencerProfileId == influencerId);

        if (conversation is null)
        {
            conversation = new Conversation
            {
                BrandProfileId = brand.Id,
                InfluencerProfileId = influencerId,
                CampaignId = campaignId
            };

            db.Conversations.Add(conversation);
            await db.SaveChangesAsync();
        }

        return RedirectToAction("Index", new { open = conversation.Id });
    }

    public async Task<IActionResult> Widget(int influencerId)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return Unauthorized();
        }

        var creator = await db.InfluencerProfiles.FirstOrDefaultAsync(i => i.Id == influencerId);
        if (creator is null)
        {
            return NotFound();
        }

        var conversation = await db.Conversations
            .Include(c => c.Messages).ThenInclude(m => m.SenderUser)
            .FirstOrDefaultAsync(c => c.BrandProfileId == brand.Id && c.InfluencerProfileId == influencerId);

        if (conversation is null)
        {
            conversation = new Conversation { BrandProfileId = brand.Id, InfluencerProfileId = influencerId };
            db.Conversations.Add(conversation);
            await db.SaveChangesAsync();
        }

        var userId = userManager.GetUserId(User);
        var unread = conversation.Messages.Where(m => m.SenderUserId != userId && m.ReadAt == null).ToList();
        if (unread.Count > 0)
        {
            foreach (var message in unread)
            {
                message.ReadAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
        }

        ViewData["ConversationId"] = conversation.Id;
        ViewData["CreatorName"] = creator.FullName;
        ViewData["CreatorId"] = creator.Id;

        return PartialView("_WidgetMessages", conversation.Messages.OrderBy(m => m.SentAt).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> SendWidget(int influencerId, string? body, IFormFile? mediaFile)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return Unauthorized();
        }

        var conversation = await db.Conversations
            .Include(c => c.InfluencerProfile)
            .Include(c => c.Messages).ThenInclude(m => m.SenderUser)
            .FirstOrDefaultAsync(c => c.BrandProfileId == brand.Id && c.InfluencerProfileId == influencerId);

        if (conversation is null)
        {
            var creatorExists = await db.InfluencerProfiles.AnyAsync(i => i.Id == influencerId);
            if (!creatorExists)
            {
                return NotFound();
            }

            conversation = new Conversation { BrandProfileId = brand.Id, InfluencerProfileId = influencerId };
            db.Conversations.Add(conversation);
            await db.SaveChangesAsync();
            await db.Entry(conversation).Reference(c => c.InfluencerProfile).LoadAsync();
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
                conversation.InfluencerProfile.UserId,
                "Message",
                "New message",
                $"You have a new message from {brand.CompanyName}.",
                $"/InfluencerMessages?open={conversation.Id}");
            await db.SaveChangesAsync();
        }

        ViewData["ConversationId"] = conversation.Id;
        ViewData["CreatorName"] = conversation.InfluencerProfile.FullName;
        ViewData["CreatorId"] = conversation.InfluencerProfileId;

        return PartialView("_WidgetMessages", conversation.Messages.OrderBy(m => m.SentAt).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditMessageWidget(int messageId, int influencerId, string? body)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return Unauthorized();
        }

        var userId = userManager.GetUserId(User);
        var message = await db.Messages
            .Include(m => m.Conversation).ThenInclude(c => c.InfluencerProfile)
            .FirstOrDefaultAsync(m => m.Id == messageId && m.Conversation.BrandProfileId == brand.Id);

        if (message is not null && message.SenderUserId == userId && string.IsNullOrEmpty(message.MediaUrl))
        {
            var trimmed = body?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(trimmed))
            {
                message.Body = trimmed;
                await db.SaveChangesAsync();
            }
        }

        return await WidgetMessagesPartial(brand.Id, influencerId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMessageWidget(int messageId, int influencerId)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return Unauthorized();
        }

        var userId = userManager.GetUserId(User);
        var message = await db.Messages
            .Include(m => m.Conversation)
            .FirstOrDefaultAsync(m => m.Id == messageId && m.Conversation.BrandProfileId == brand.Id);

        if (message is not null && message.SenderUserId == userId)
        {
            db.Messages.Remove(message);
            await db.SaveChangesAsync();
        }

        return await WidgetMessagesPartial(brand.Id, influencerId);
    }

    private async Task<IActionResult> WidgetMessagesPartial(int brandId, int influencerId)
    {
        var conversation = await db.Conversations
            .Include(c => c.InfluencerProfile)
            .Include(c => c.Messages).ThenInclude(m => m.SenderUser)
            .FirstOrDefaultAsync(c => c.BrandProfileId == brandId && c.InfluencerProfileId == influencerId);

        if (conversation is null)
        {
            return NotFound();
        }

        ViewData["ConversationId"] = conversation.Id;
        ViewData["CreatorName"] = conversation.InfluencerProfile.FullName;
        ViewData["CreatorId"] = conversation.InfluencerProfileId;

        return PartialView("_WidgetMessages", conversation.Messages.OrderBy(m => m.SentAt).ToList());
    }
}
