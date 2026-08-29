using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brandora.Web.Migrations
{
    /// <inheritdoc />
    public partial class CampaignMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MediaType",
                table: "Campaigns",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaUrl",
                table: "Campaigns",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MediaType",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "MediaUrl",
                table: "Campaigns");
        }
    }
}
