using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Brandora.Web.Models.Messages;
using Brandora.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

public class MessagesController(UserManager<ApplicationUser> userManager, ApplicationDbContext db, MediaUploadService mediaUploads) : BrandControllerBase(userManager, db)
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

        var conversation = await db.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId && c.BrandProfileId == brand.Id);
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

        if (!string.IsNullOrEmpty(trimmedBody) || mediaUrl is not null)
        {
            db.Messages.Add(new Message
            {
                ConversationId = conversation.Id,
                SenderUserId = userManager.GetUserId(User)!,
                Body = trimmedBody,
                MediaUrl = mediaUrl,
                MediaType = mediaType
            });

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

        var conversation = await db.Conversations.FirstOrDefaultAsync(c =>
            c.BrandProfileId == brand.Id && c.InfluencerProfileId == influencerId && c.CampaignId == campaignId);

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
}
