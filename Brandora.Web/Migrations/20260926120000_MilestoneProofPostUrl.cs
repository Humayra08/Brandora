using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brandora.Web.Migrations
{
    /// <inheritdoc />
    public partial class MilestoneProofPostUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Link to the creator's live social media post for a milestone, stored next to
            // the proof file so both are kept when a creator submits a file and a link.
            migrationBuilder.AddColumn<string>(
                name: "ProofPostUrl",
                table: "Milestones",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProofPostUrl",
                table: "Milestones");
        }
    }
}
