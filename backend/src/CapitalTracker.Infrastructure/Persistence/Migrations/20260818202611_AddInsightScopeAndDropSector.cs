using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInsightScopeAndDropSector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The only sector-scoped rows that ever existed are the two placeholders the
            // old stub wrote ("AI-аналіз цього сектору ще не підключено"). They hold no
            // analysis at all, and once SectorId is gone they would sit in the archive
            // indistinguishable from real runs. Deleted here, before the column is.
            migrationBuilder.Sql(@"DELETE FROM ""AiInsights"" WHERE ""SectorId"" IS NOT NULL;");

            migrationBuilder.DropForeignKey(
                name: "FK_AiInsights_Holdings_HoldingId",
                table: "AiInsights");

            migrationBuilder.DropForeignKey(
                name: "FK_AiInsights_Sectors_SectorId",
                table: "AiInsights");

            migrationBuilder.DropIndex(
                name: "IX_AiInsights_SectorId",
                table: "AiInsights");

            migrationBuilder.DropColumn(
                name: "SectorId",
                table: "AiInsights");

            migrationBuilder.AddColumn<int>(
                name: "Scope",
                table: "AiInsights",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "FK_AiInsights_Holdings_HoldingId",
                table: "AiInsights",
                column: "HoldingId",
                principalTable: "Holdings",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiInsights_Holdings_HoldingId",
                table: "AiInsights");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "AiInsights");

            migrationBuilder.AddColumn<Guid>(
                name: "SectorId",
                table: "AiInsights",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiInsights_SectorId",
                table: "AiInsights",
                column: "SectorId");

            migrationBuilder.AddForeignKey(
                name: "FK_AiInsights_Holdings_HoldingId",
                table: "AiInsights",
                column: "HoldingId",
                principalTable: "Holdings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AiInsights_Sectors_SectorId",
                table: "AiInsights",
                column: "SectorId",
                principalTable: "Sectors",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
