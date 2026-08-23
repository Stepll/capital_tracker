using CapitalTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalTracker.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Moves "how many units are held" out of Holdings.Quantity and into the Transactions
    /// table, which becomes the only place it lives.
    ///
    /// The column can't just be dropped: its value is real data. Every existing holding gets
    /// an opening Buy carrying the units it had, priced from its earliest valuation — the
    /// closest thing to a cost basis that exists at this point, and editable afterwards.
    ///
    /// Two cases deliberately get no row, mirroring CreateHoldingCommand:
    ///   - a quotable holding (ticker + brokerage) with no quantity — inventing one share
    ///     would let the price job multiply a quote by it and rewrite the value;
    ///   - a holding that already has transactions — nothing was lost there to restore.
    /// Everything else, an apartment included, is one indivisible unit, so its purchase
    /// shows up in the account's history like everything else.
    /// </summary>
    public partial class MakeTransactionsTheSourceOfQuantity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_HoldingId",
                table: "Transactions");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitPrice",
                table: "Transactions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "Transactions",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "Transactions",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_HoldingId_Date",
                table: "Transactions",
                columns: new[] { "HoldingId", "Date" });

            // Runs before the column is dropped, obviously — and past the soft-delete
            // filters, because a deleted holding's history is exactly what soft deletion
            // was introduced to preserve. Type 0 is TransactionType.Buy; Accounts."Type" 0
            // is AccountType.Brokerage.
            migrationBuilder.Sql($"""
                INSERT INTO "Transactions" ("Id", "HoldingId", "Type", "Date", "Quantity", "UnitPrice", "Currency", "Notes")
                SELECT
                    gen_random_uuid(),
                    h."Id",
                    0,
                    COALESCE(opening."Date", (h."CreatedAt" AT TIME ZONE 'UTC')::date),
                    COALESCE(h."Quantity", 1),
                    ROUND(COALESCE(opening."Value", 0) / COALESCE(h."Quantity", 1), 2),
                    COALESCE(opening."Currency", a."Currency"),
                    '{Transaction.OpeningPositionNote}'
                FROM "Holdings" h
                JOIN "Accounts" a ON a."Id" = h."AccountId"
                LEFT JOIN LATERAL (
                    SELECT v."Date", v."Value", v."Currency"
                    FROM "ValuationSnapshots" v
                    WHERE v."HoldingId" = h."Id"
                    ORDER BY v."Date"
                    LIMIT 1
                ) opening ON TRUE
                WHERE NOT EXISTS (SELECT 1 FROM "Transactions" t WHERE t."HoldingId" = h."Id")
                  AND COALESCE(h."Quantity", 1) > 0
                  AND (h."Quantity" IS NOT NULL OR h."Symbol" IS NULL OR a."Type" <> 0);
                """);

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "Holdings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_HoldingId_Date",
                table: "Transactions");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitPrice",
                table: "Transactions",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "Transactions",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,10)",
                oldPrecision: 28,
                oldScale: 10);

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "Transactions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3);

            migrationBuilder.AddColumn<decimal>(
                name: "Quantity",
                table: "Holdings",
                type: "numeric(28,10)",
                precision: 28,
                scale: 10,
                nullable: true);

            // Restores the column from the positions the transactions now describe, rather
            // than leaving it null: rolling back should give the old code its numbers back.
            // The transactions themselves stay — deleting rows on the way down would throw
            // away anything entered since. Type codes: Buy 0, Sell 1, Deposit 5, Withdrawal 6.
            migrationBuilder.Sql("""
                UPDATE "Holdings" h
                SET "Quantity" = position."Units"
                FROM (
                    SELECT
                        "HoldingId",
                        SUM(CASE WHEN "Type" IN (0, 5) THEN "Quantity" ELSE -"Quantity" END) AS "Units"
                    FROM "Transactions"
                    WHERE "Type" IN (0, 1, 5, 6)
                    GROUP BY "HoldingId"
                ) position
                WHERE h."Id" = position."HoldingId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_HoldingId",
                table: "Transactions",
                column: "HoldingId");
        }
    }
}
