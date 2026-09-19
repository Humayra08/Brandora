using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brandora.Web.Migrations
{
    /// <inheritdoc />
    public partial class MergeDuplicateConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Conversations used to be keyed by (Brand, Influencer, Campaign),
            // so the same two people could end up with a separate thread per
            // campaign. Going forward a conversation is keyed by (Brand,
            // Influencer) only, so this merges any pre-existing duplicates:
            // for each pair, keep the earliest conversation, move every
            // message from the later duplicates onto it, then drop the
            // now-empty duplicate rows.
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "Id", "BrandProfileId", "InfluencerProfileId",
                           FIRST_VALUE("Id") OVER (
                               PARTITION BY "BrandProfileId", "InfluencerProfileId"
                               ORDER BY "Id"
                           ) AS "CanonicalId"
                    FROM "Conversations"
                )
                UPDATE "Messages" m
                SET "ConversationId" = r."CanonicalId"
                FROM ranked r
                WHERE m."ConversationId" = r."Id" AND r."Id" <> r."CanonicalId";
                """);

            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "Id",
                           FIRST_VALUE("Id") OVER (
                               PARTITION BY "BrandProfileId", "InfluencerProfileId"
                               ORDER BY "Id"
                           ) AS "CanonicalId"
                    FROM "Conversations"
                )
                DELETE FROM "Conversations" c
                USING ranked r
                WHERE c."Id" = r."Id" AND r."Id" <> r."CanonicalId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Merging duplicate conversations/messages is a one-way data
            // cleanup — the original per-campaign split can't be
            // reconstructed once messages have been reassigned.
        }
    }
}
