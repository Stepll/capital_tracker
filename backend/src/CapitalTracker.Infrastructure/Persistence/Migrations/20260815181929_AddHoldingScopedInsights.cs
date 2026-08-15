using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHoldingScopedInsights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiInsights_Sectors_SectorId",
                table: "AiInsights");

            migrationBuilder.AlterColumn<Guid>(
                name: "SectorId",
                table: "AiInsights",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "HoldingId",
                table: "AiInsights",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiInsights_HoldingId",
                table: "AiInsights",
                column: "HoldingId");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiInsights_Holdings_HoldingId",
                table: "AiInsights");

            migrationBuilder.DropForeignKey(
                name: "FK_AiInsights_Sectors_SectorId",
                table: "AiInsights");

            migrationBuilder.DropIndex(
                name: "IX_AiInsights_HoldingId",
                table: "AiInsights");

            migrationBuilder.DropColumn(
                name: "HoldingId",
                table: "AiInsights");

            migrationBuilder.AlterColumn<Guid>(
                name: "SectorId",
                table: "AiInsights",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AiInsights_Sectors_SectorId",
                table: "AiInsights",
                column: "SectorId",
                principalTable: "Sectors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
