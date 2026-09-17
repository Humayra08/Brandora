using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brandora.Web.Migrations
{
    /// <inheritdoc />
    public partial class MilestoneContentType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "Milestones",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "CampaignMilestonePlans",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "Milestones");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "CampaignMilestonePlans");
        }
    }
}
