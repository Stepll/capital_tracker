using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddValuationSnapshotUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-added, and it must run before CreateIndex below. Production already
            // violates the invariant this index introduces — one holding accumulated
            // three rows for the same date — so a bare CREATE UNIQUE INDEX would fail,
            // and since the Api applies migrations at startup that means a crash-looping
            // container rather than just a failed command.
            //
            // Tie-break, in order: a manual row beats an automatic one (never discard a
            // number someone typed), then the physically newest row wins. ctid is the
            // only ordering signal available — Id is a random Guid and the table carries
            // no timestamp — and on an insert-mostly table it tracks insertion order
            // closely enough for a one-off cleanup of a handful of rows.
            migrationBuilder.Sql("""
                DELETE FROM "ValuationSnapshots" a
                USING "ValuationSnapshots" b
                WHERE a."HoldingId" = b."HoldingId"
                  AND a."Date" = b."Date"
                  AND (a."IsManual", a.ctid) < (b."IsManual", b.ctid);
                """);

            migrationBuilder.DropIndex(
                name: "IX_ValuationSnapshots_HoldingId",
                table: "ValuationSnapshots");

            migrationBuilder.AlterColumn<decimal>(
                name: "Value",
                table: "ValuationSnapshots",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "ValuationSnapshots",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_ValuationSnapshots_HoldingId_Date",
                table: "ValuationSnapshots",
                columns: new[] { "HoldingId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ValuationSnapshots_HoldingId_Date",
                table: "ValuationSnapshots");

            migrationBuilder.AlterColumn<decimal>(
                name: "Value",
                table: "ValuationSnapshots",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "ValuationSnapshots",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3);

            migrationBuilder.CreateIndex(
                name: "IX_ValuationSnapshots_HoldingId",
                table: "ValuationSnapshots",
                column: "HoldingId");
        }
    }
}
