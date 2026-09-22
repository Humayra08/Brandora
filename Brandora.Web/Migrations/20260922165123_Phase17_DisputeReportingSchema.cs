using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Brandora.Web.Migrations
{
    /// <inheritdoc />
    public partial class Phase17_DisputeReportingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BrandStatement",
                table: "Disputes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BrandStatementAt",
                table: "Disputes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InfluencerStatement",
                table: "Disputes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InfluencerStatementAt",
                table: "Disputes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RaisedBy",
                table: "Disputes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DisputeEvidence",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DisputeId = table.Column<int>(type: "integer", nullable: false),
                    UploadedBy = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    FileUrl = table.Column<string>(type: "text", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisputeEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DisputeEvidence_Disputes_DisputeId",
                        column: x => x.DisputeId,
                        principalTable: "Disputes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DisputeNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DisputeId = table.Column<int>(type: "integer", nullable: false),
                    AdminName = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisputeNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DisputeNotes_Disputes_DisputeId",
                        column: x => x.DisputeId,
                        principalTable: "Disputes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DisputeEvidence_DisputeId",
                table: "DisputeEvidence",
                column: "DisputeId");

            migrationBuilder.CreateIndex(
                name: "IX_DisputeNotes_DisputeId",
                table: "DisputeNotes",
                column: "DisputeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DisputeEvidence");

            migrationBuilder.DropTable(
                name: "DisputeNotes");

            migrationBuilder.DropColumn(
                name: "BrandStatement",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "BrandStatementAt",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "InfluencerStatement",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "InfluencerStatementAt",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "RaisedBy",
                table: "Disputes");
        }
    }
}
