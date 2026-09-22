using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Brandora.Web.Migrations
{
    /// <inheritdoc />
    public partial class Phase12_WithdrawalPayoutSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProcessedByAdmin",
                table: "WithdrawalRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransactionReference",
                table: "WithdrawalRequests",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PlatformWalletTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    AccountDetail = table.Column<string>(type: "text", nullable: false),
                    GatewayReference = table.Column<string>(type: "text", nullable: false),
                    RecipientBrandProfileId = table.Column<int>(type: "integer", nullable: true),
                    RecipientInfluencerProfileId = table.Column<int>(type: "integer", nullable: true),
                    DisputeId = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "text", nullable: true),
                    ProcessedByAdmin = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformWalletTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlatformWalletTransactions_BrandProfiles_RecipientBrandProf~",
                        column: x => x.RecipientBrandProfileId,
                        principalTable: "BrandProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlatformWalletTransactions_Disputes_DisputeId",
                        column: x => x.DisputeId,
                        principalTable: "Disputes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlatformWalletTransactions_InfluencerProfiles_RecipientInfl~",
                        column: x => x.RecipientInfluencerProfileId,
                        principalTable: "InfluencerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformWalletTransactions_DisputeId",
                table: "PlatformWalletTransactions",
                column: "DisputeId");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformWalletTransactions_RecipientBrandProfileId",
                table: "PlatformWalletTransactions",
                column: "RecipientBrandProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformWalletTransactions_RecipientInfluencerProfileId",
                table: "PlatformWalletTransactions",
                column: "RecipientInfluencerProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformWalletTransactions");

            migrationBuilder.DropColumn(
                name: "ProcessedByAdmin",
                table: "WithdrawalRequests");

            migrationBuilder.DropColumn(
                name: "TransactionReference",
                table: "WithdrawalRequests");
        }
    }
}
