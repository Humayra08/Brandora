using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Brandora.Web.Migrations
{
    /// <inheritdoc />
    public partial class CampaignWizardSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TargetEngagementRateMin",
                table: "Campaigns",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetFollowersMax",
                table: "Campaigns",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetFollowersMin",
                table: "Campaigns",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetLocation",
                table: "Campaigns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TargetVerifiedOnly",
                table: "Campaigns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TargetingConfigured",
                table: "Campaigns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CampaignMilestonePlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CampaignId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignMilestonePlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignMilestonePlans_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMilestonePlans_CampaignId",
                table: "CampaignMilestonePlans",
                column: "CampaignId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignMilestonePlans");

            migrationBuilder.DropColumn(
                name: "TargetEngagementRateMin",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "TargetFollowersMax",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "TargetFollowersMin",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "TargetLocation",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "TargetVerifiedOnly",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "TargetingConfigured",
                table: "Campaigns");
        }
    }
}
