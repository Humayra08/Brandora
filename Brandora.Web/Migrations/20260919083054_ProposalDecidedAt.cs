using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brandora.Web.Migrations
{
    /// <inheritdoc />
    public partial class ProposalDecidedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DecidedAt",
                table: "Proposals",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DecidedAt",
                table: "Proposals");
        }
    }
}
