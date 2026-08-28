using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

public class MessagesController(UserManager<ApplicationUser> userManager, ApplicationDbContext db) : BrandControllerBase(userManager, db)
{
    public async Task<IActionResult> Index(int? campaignId)
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

        var ordered = conversations
            .OrderByDescending(c => c.Messages.Count > 0 ? c.Messages.Max(m => m.SentAt) : c.CreatedAt)
            .ToList();

        return View(ordered);
    }

    public async Task<IActionResult> Conversation(int id)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var conversation = await db.Conversations
            .Include(c => c.InfluencerProfile)
            .Include(c => c.Campaign)
            .Include(c => c.Messages).ThenInclude(m => m.SenderUser)
            .FirstOrDefaultAsync(c => c.Id == id && c.BrandProfileId == brand.Id);

        if (conversation is null)
        {
            return NotFound();
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

        return View(conversation);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(int conversationId, string body)
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

        if (!string.IsNullOrWhiteSpace(body))
        {
            db.Messages.Add(new Message
            {
                ConversationId = conversation.Id,
                SenderUserId = userManager.GetUserId(User)!,
                Body = body.Trim()
            });

            await db.SaveChangesAsync();
        }

        return RedirectToAction("Conversation", new { id = conversationId });
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

        return RedirectToAction("Conversation", new { id = conversation.Id });
    }
}
