using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixAiInsightDeleteBehavior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiInsights_Holdings_HoldingId",
                table: "AiInsights");

            migrationBuilder.DropForeignKey(
                name: "FK_AiInsights_Sectors_SectorId",
                table: "AiInsights");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiInsights_Holdings_HoldingId",
                table: "AiInsights");

            migrationBuilder.DropForeignKey(
                name: "FK_AiInsights_Sectors_SectorId",
                table: "AiInsights");

            migrationBuilder.AddForeignKey(
                name: "FK_AiInsights_Holdings_HoldingId",
                table: "AiInsights",
                column: "HoldingId",
                principalTable: "Holdings",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AiInsights_Sectors_SectorId",
                table: "AiInsights",
                column: "SectorId",
                principalTable: "Sectors",
                principalColumn: "Id");
        }
    }
}
