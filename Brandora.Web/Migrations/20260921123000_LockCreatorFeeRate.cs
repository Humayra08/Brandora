using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brandora.Web.Migrations
{
    /// <inheritdoc />
    public partial class LockCreatorFeeRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The creator's commission rate, locked on each payment at release. Existing
            // rows stay NULL ("not locked yet") and use the configured rate until settled.
            migrationBuilder.AddColumn<decimal>(
                name: "CreatorFeePercent",
                table: "Payments",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CreatorFeePercent", table: "Payments");
        }
    }
}
