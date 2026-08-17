using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiAnalysisFacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-edited after scaffolding — two changes, both deliberate:
            //
            // 1. Drop every holding-scoped insight. They are all placeholder text from
            //    the stub generator, so there is nothing to preserve; and they are
            //    exactly the rows that carry BOTH HoldingId and SectorId, which is the
            //    bug that leaked per-asset insights into the sector feed (GetInsightsQuery
            //    filters on SectorId != null). Clearing them here means the leak has no
            //    historical residue once the real pipeline — which only ever sets
            //    HoldingId — takes over. Sector-scoped stubs are left alone; that feed
            //    is still stubbed by design.
            migrationBuilder.Sql("DELETE FROM \"AiInsights\" WHERE \"HoldingId\" IS NOT NULL;");

            migrationBuilder.AddColumn<bool>(
                name: "ExcludeFromAiAnalysis",
                table: "Holdings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // 2. EF scaffolds defaultValue: "" for a new non-nullable string-backed
            //    column, but '' is not valid jsonb — the ALTER TABLE would fail outright
            //    against the rows already in production. An empty array is the correct
            //    empty value here and matches the entity's own initializer.
            migrationBuilder.AddColumn<string>(
                name: "Facts",
                table: "AiInsights",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExcludeFromAiAnalysis",
                table: "Holdings");

            migrationBuilder.DropColumn(
                name: "Facts",
                table: "AiInsights");
        }
    }
}
