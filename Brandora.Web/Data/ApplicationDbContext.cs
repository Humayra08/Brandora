using Brandora.Web.Models.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<BrandProfile> BrandProfiles => Set<BrandProfile>();
    public DbSet<InfluencerProfile> InfluencerProfiles => Set<InfluencerProfile>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<Proposal> Proposals => Set<Proposal>();
    public DbSet<Collaboration> Collaborations => Set<Collaboration>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ShortlistEntry> ShortlistEntries => Set<ShortlistEntry>();
    public DbSet<Dispute> Disputes => Set<Dispute>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<BrandProfile>(entity =>
        {
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.HasOne(e => e.User)
                .WithOne(u => u.BrandProfile)
                .HasForeignKey<BrandProfile>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<InfluencerProfile>(entity =>
        {
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.Property(e => e.EngagementRate).HasPrecision(5, 2);

            entity.HasOne(e => e.User)
                .WithOne(u => u.InfluencerProfile)
                .HasForeignKey<InfluencerProfile>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ShortlistEntry>(entity =>
        {
            entity.HasIndex(e => new { e.BrandProfileId, e.InfluencerProfileId }).IsUnique();

            entity.HasOne(e => e.BrandProfile)
                .WithMany(b => b.ShortlistEntries)
                .HasForeignKey(e => e.BrandProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.InfluencerProfile)
                .WithMany(i => i.ShortlistedBy)
                .HasForeignKey(e => e.InfluencerProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Campaign>(entity =>
        {
            entity.Property(e => e.Budget).HasPrecision(12, 2);
            entity.Property(e => e.SpentAmount).HasPrecision(12, 2);

            entity.HasOne(e => e.BrandProfile)
                .WithMany(b => b.Campaigns)
                .HasForeignKey(e => e.BrandProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Proposal>(entity =>
        {
            entity.Property(e => e.ProposedAmount).HasPrecision(12, 2);

            entity.HasOne(e => e.Campaign)
                .WithMany(c => c.Proposals)
                .HasForeignKey(e => e.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.InfluencerProfile)
                .WithMany(i => i.Proposals)
                .HasForeignKey(e => e.InfluencerProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Collaboration>(entity =>
        {
            entity.HasIndex(e => e.ProposalId).IsUnique();

            entity.HasOne(e => e.Proposal)
                .WithOne(p => p.Collaboration)
                .HasForeignKey<Collaboration>(e => e.ProposalId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Campaign)
                .WithMany()
                .HasForeignKey(e => e.CampaignId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.InfluencerProfile)
                .WithMany()
                .HasForeignKey(e => e.InfluencerProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Milestone>(entity =>
        {
            entity.Property(e => e.Amount).HasPrecision(12, 2);

            entity.HasOne(e => e.Collaboration)
                .WithMany(c => c.Milestones)
                .HasForeignKey(e => e.CollaborationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Conversation>(entity =>
        {
            entity.HasOne(e => e.Campaign)
                .WithMany(c => c.Conversations)
                .HasForeignKey(e => e.CampaignId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.BrandProfile)
                .WithMany(b => b.Conversations)
                .HasForeignKey(e => e.BrandProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.InfluencerProfile)
                .WithMany(i => i.Conversations)
                .HasForeignKey(e => e.InfluencerProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Message>(entity =>
        {
            entity.HasOne(e => e.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(e => e.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.SenderUser)
                .WithMany()
                .HasForeignKey(e => e.SenderUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Payment>(entity =>
        {
            entity.Property(e => e.Amount).HasPrecision(12, 2);
            entity.HasIndex(e => e.MilestoneId).IsUnique(false);

            entity.HasOne(e => e.Collaboration)
                .WithMany(c => c.Payments)
                .HasForeignKey(e => e.CollaborationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Milestone)
                .WithOne(m => m.Payment)
                .HasForeignKey<Payment>(e => e.MilestoneId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Notification>(entity =>
        {
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Dispute>(entity =>
        {
            entity.HasOne(e => e.Collaboration)
                .WithMany()
                .HasForeignKey(e => e.CollaborationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Milestone)
                .WithMany()
                .HasForeignKey(e => e.MilestoneId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.BrandProfile)
                .WithMany()
                .HasForeignKey(e => e.BrandProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.InfluencerProfile)
                .WithMany()
                .HasForeignKey(e => e.InfluencerProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
