using Brandora.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brandora.Web.Migrations
{
    /// <summary>
    /// Data-only fix: a brand and a creator must share ONE conversation. Accepting a
    /// collaboration used to open a second thread with the same creator (one per campaign),
    /// which showed up as a duplicate chat (after the earlier MergeDuplicateConversations
    /// clean-up had already run). This folds every duplicate into the pair's
    /// oldest thread — all messages kept, in order — and points notification links at it.
    /// No schema change, so there is no model snapshot for this migration.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260926130000_MergeConversationsPerBrandCreator")]
    public partial class MergeConversationsPerBrandCreator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Carry each side's state onto the thread that is kept: pinned if any copy was
            //    pinned; "deleted" only if that side had deleted every copy (earliest time),
            //    so no message that side could still see gets hidden by the merge.
            migrationBuilder.Sql("""
                UPDATE "Conversations" AS c
                SET "CampaignId" = COALESCE(c."CampaignId", g.campaign_id),
                    "BrandPinnedAt" = g.brand_pinned,
                    "InfluencerPinnedAt" = g.influencer_pinned,
                    "BrandClearedAt" = g.brand_cleared,
                    "InfluencerClearedAt" = g.influencer_cleared
                FROM (
                    SELECT MIN("Id") AS keep_id,
                           MIN("CampaignId") AS campaign_id,
                           MAX("BrandPinnedAt") AS brand_pinned,
                           MAX("InfluencerPinnedAt") AS influencer_pinned,
                           CASE WHEN COUNT(*) = COUNT("BrandClearedAt") THEN MIN("BrandClearedAt") END AS brand_cleared,
                           CASE WHEN COUNT(*) = COUNT("InfluencerClearedAt") THEN MIN("InfluencerClearedAt") END AS influencer_cleared
                    FROM "Conversations"
                    GROUP BY "BrandProfileId", "InfluencerProfileId"
                    HAVING COUNT(*) > 1
                ) AS g
                WHERE c."Id" = g.keep_id;
                """);

            // 2. Move every message into the kept thread.
            migrationBuilder.Sql("""
                UPDATE "Messages" AS m
                SET "ConversationId" = d.keep_id
                FROM (
                    SELECT "Id", MIN("Id") OVER (PARTITION BY "BrandProfileId", "InfluencerProfileId") AS keep_id
                    FROM "Conversations"
                ) AS d
                WHERE m."ConversationId" = d."Id" AND d."Id" <> d.keep_id;
                """);

            // 3. Notifications that opened a duplicate now open the kept thread.
            migrationBuilder.Sql("""
                UPDATE "Notifications" AS n
                SET "LinkUrl" = CASE
                        WHEN n."LinkUrl" LIKE '/InfluencerMessages?open=%' THEN '/InfluencerMessages?open=' || d.keep_id
                        ELSE '/Messages?open=' || d.keep_id
                    END
                FROM (
                    SELECT "Id", MIN("Id") OVER (PARTITION BY "BrandProfileId", "InfluencerProfileId") AS keep_id
                    FROM "Conversations"
                ) AS d
                WHERE d."Id" <> d.keep_id
                  AND (n."LinkUrl" = '/Messages?open=' || d."Id" OR n."LinkUrl" = '/InfluencerMessages?open=' || d."Id");
                """);

            // 4. Remove the now-empty duplicates.
            migrationBuilder.Sql("""
                DELETE FROM "Conversations" AS c
                USING (
                    SELECT "Id", MIN("Id") OVER (PARTITION BY "BrandProfileId", "InfluencerProfileId") AS keep_id
                    FROM "Conversations"
                ) AS d
                WHERE c."Id" = d."Id" AND d."Id" <> d.keep_id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Merged threads can't be split back apart; nothing to undo.
        }
    }
}
