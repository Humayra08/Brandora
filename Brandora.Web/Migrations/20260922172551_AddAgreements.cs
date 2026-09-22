using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Brandora.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddAgreements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Agreements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<decimal>(type: "numeric", nullable: false),
                    CampaignId = table.Column<int>(type: "integer", nullable: false),
                    ProposalId = table.Column<int>(type: "integer", nullable: false),
                    BrandProfileId = table.Column<int>(type: "integer", nullable: false),
                    InfluencerProfileId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ContentHtml = table.Column<string>(type: "text", nullable: false),
                    ContentHash = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Agreements_BrandProfiles_BrandProfileId",
                        column: x => x.BrandProfileId,
                        principalTable: "BrandProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Agreements_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Agreements_InfluencerProfiles_InfluencerProfileId",
                        column: x => x.InfluencerProfileId,
                        principalTable: "InfluencerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Agreements_Proposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "Proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AgreementEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AgreementId = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Detail = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgreementEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgreementEvents_Agreements_AgreementId",
                        column: x => x.AgreementId,
                        principalTable: "Agreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgreementSignatures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AgreementId = table.Column<int>(type: "integer", nullable: false),
                    Party = table.Column<int>(type: "integer", nullable: false),
                    SignerUserId = table.Column<string>(type: "text", nullable: false),
                    SignerName = table.Column<string>(type: "text", nullable: false),
                    SignatureImageUrl = table.Column<string>(type: "text", nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    SignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IpAddress = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgreementSignatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgreementSignatures_Agreements_AgreementId",
                        column: x => x.AgreementId,
                        principalTable: "Agreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgreementEvents_AgreementId",
                table: "AgreementEvents",
                column: "AgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_Agreements_BrandProfileId",
                table: "Agreements",
                column: "BrandProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Agreements_CampaignId",
                table: "Agreements",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_Agreements_Code",
                table: "Agreements",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Agreements_InfluencerProfileId",
                table: "Agreements",
                column: "InfluencerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Agreements_ProposalId",
                table: "Agreements",
                column: "ProposalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgreementSignatures_AgreementId",
                table: "AgreementSignatures",
                column: "AgreementId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgreementEvents");

            migrationBuilder.DropTable(
                name: "AgreementSignatures");

            migrationBuilder.DropTable(
                name: "Agreements");
        }
    }
}
