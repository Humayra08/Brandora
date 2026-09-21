using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brandora.Web.Migrations
{
    /// <inheritdoc />
    public partial class TwoSidedCommissionConversationPrefsCampaignVideo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- Two-sided commission ----
            migrationBuilder.AddColumn<decimal>(
                name: "BrandFeeAmount",
                table: "Payments",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FeePercent",
                table: "WithdrawalRequests",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FeeAmount",
                table: "WithdrawalRequests",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PayoutAmount",
                table: "WithdrawalRequests",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // Withdrawals requested before fees existed were fee-free: the creator receives
            // exactly what they asked for.
            migrationBuilder.Sql("UPDATE \"WithdrawalRequests\" SET \"PayoutAmount\" = \"Amount\";");

            // ---- Per-side conversation pin / delete ----
            migrationBuilder.AddColumn<DateTime>(
                name: "BrandClearedAt",
                table: "Conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BrandPinnedAt",
                table: "Conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InfluencerClearedAt",
                table: "Conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InfluencerPinnedAt",
                table: "Conversations",
                type: "timestamp with time zone",
                nullable: true);

            // ---- Campaign banner and video as separate files ----
            migrationBuilder.AddColumn<string>(
                name: "VideoUrl",
                table: "Campaigns",
                type: "text",
                nullable: true);

            // A campaign whose single "creative" was a video keeps it — as its video. The
            // banner slot is image-only from now on.
            migrationBuilder.Sql(
                "UPDATE \"Campaigns\" SET \"VideoUrl\" = \"MediaUrl\", \"MediaUrl\" = NULL, \"MediaType\" = NULL " +
                "WHERE \"MediaType\" = 'video' AND \"MediaUrl\" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE \"Campaigns\" SET \"MediaUrl\" = \"VideoUrl\", \"MediaType\" = 'video' " +
                "WHERE \"VideoUrl\" IS NOT NULL AND \"MediaUrl\" IS NULL;");

            migrationBuilder.DropColumn(name: "VideoUrl", table: "Campaigns");

            migrationBuilder.DropColumn(name: "BrandClearedAt", table: "Conversations");
            migrationBuilder.DropColumn(name: "BrandPinnedAt", table: "Conversations");
            migrationBuilder.DropColumn(name: "InfluencerClearedAt", table: "Conversations");
            migrationBuilder.DropColumn(name: "InfluencerPinnedAt", table: "Conversations");

            migrationBuilder.DropColumn(name: "PayoutAmount", table: "WithdrawalRequests");
            migrationBuilder.DropColumn(name: "FeeAmount", table: "WithdrawalRequests");
            migrationBuilder.DropColumn(name: "FeePercent", table: "WithdrawalRequests");

            migrationBuilder.DropColumn(name: "BrandFeeAmount", table: "Payments");
        }
    }
}
