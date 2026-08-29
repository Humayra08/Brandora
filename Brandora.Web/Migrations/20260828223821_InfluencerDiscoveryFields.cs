using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brandora.Web.Migrations
{
    /// <inheritdoc />
    public partial class InfluencerDiscoveryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Bio",
                table: "InfluencerProfiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EngagementRate",
                table: "InfluencerProfiles",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Followers",
                table: "InfluencerProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "InfluencerProfiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RateNote",
                table: "InfluencerProfiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ShortlistEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BrandProfileId = table.Column<int>(type: "int", nullable: false),
                    InfluencerProfileId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShortlistEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShortlistEntries_BrandProfiles_BrandProfileId",
                        column: x => x.BrandProfileId,
                        principalTable: "BrandProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShortlistEntries_InfluencerProfiles_InfluencerProfileId",
                        column: x => x.InfluencerProfileId,
                        principalTable: "InfluencerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShortlistEntries_BrandProfileId_InfluencerProfileId",
                table: "ShortlistEntries",
                columns: new[] { "BrandProfileId", "InfluencerProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShortlistEntries_InfluencerProfileId",
                table: "ShortlistEntries",
                column: "InfluencerProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShortlistEntries");

            migrationBuilder.DropColumn(
                name: "Bio",
                table: "InfluencerProfiles");

            migrationBuilder.DropColumn(
                name: "EngagementRate",
                table: "InfluencerProfiles");

            migrationBuilder.DropColumn(
                name: "Followers",
                table: "InfluencerProfiles");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "InfluencerProfiles");

            migrationBuilder.DropColumn(
                name: "RateNote",
                table: "InfluencerProfiles");
        }
    }
}
