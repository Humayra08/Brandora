using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brandora.Web.Migrations
{
    /// <inheritdoc />
    public partial class BrandSocialLinksAndCampaignPostUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- Brand social profile links (Settings → Social Profiles) ----
            migrationBuilder.AddColumn<string>(
                name: "FacebookUrl",
                table: "BrandProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstagramUrl",
                table: "BrandProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TikTokUrl",
                table: "BrandProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YouTubeUrl",
                table: "BrandProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkedInUrl",
                table: "BrandProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "XUrl",
                table: "BrandProfiles",
                type: "text",
                nullable: true);

            // ---- Optional link to the campaign's live social media post ----
            migrationBuilder.AddColumn<string>(
                name: "SocialPostUrl",
                table: "Campaigns",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FacebookUrl",
                table: "BrandProfiles");

            migrationBuilder.DropColumn(
                name: "InstagramUrl",
                table: "BrandProfiles");

            migrationBuilder.DropColumn(
                name: "TikTokUrl",
                table: "BrandProfiles");

            migrationBuilder.DropColumn(
                name: "YouTubeUrl",
                table: "BrandProfiles");

            migrationBuilder.DropColumn(
                name: "LinkedInUrl",
                table: "BrandProfiles");

            migrationBuilder.DropColumn(
                name: "XUrl",
                table: "BrandProfiles");

            migrationBuilder.DropColumn(
                name: "SocialPostUrl",
                table: "Campaigns");
        }
    }
}
