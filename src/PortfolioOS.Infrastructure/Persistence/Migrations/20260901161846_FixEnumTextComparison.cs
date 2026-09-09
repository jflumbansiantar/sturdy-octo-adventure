using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixEnumTextComparison : Migration
    {
        // Fixes a pre-existing bug, found while wiring up the chat assistant.
        //
        // THE BUG
        // GET /api/transactions?category=Expense returned HTTP 500 with
        // "operator does not exist: transaction_category = text". That is the category filter on
        // the Transactions page of the web client, so the filter was simply broken.
        //
        // WHY
        // InitialCreate created `text -> enum` casts so that Npgsql, which sends the
        // string-converted enum as a `text` parameter, could INSERT and UPDATE these columns.
        // Those casts are ASSIGNMENT casts, and Postgres applies assignment casts only when
        // storing a value - never when resolving an operator. So `enum = text` in a WHERE clause
        // finds no candidate operator at all and the query fails.
        //
        // THE FIX
        // Add the opposite direction, `enum -> text`, as an IMPLICIT cast. Operator resolution
        // will then coerce the column to text and use `text = text`. The existing `text -> enum`
        // assignment casts are left exactly as they were; writes keep working through them.
        //
        // TRADE-OFF (verified on a clean database, not assumed)
        // An implicit cast to text makes `<enum_column> || 'some literal'` ambiguous in raw SQL,
        // because both `text || text` and `anynonarray || text` become candidates. Everything
        // else is unaffected: comparison, INSERT, `enum = enum`, and ordinary text concatenation
        // all behave as before. Nothing in this repository concatenates an enum column - there is
        // no `||` in database/schema.sql, and the only hand-written SQL is the chat retriever -
        // so nothing is broken by this today. If such a query is ever needed, write it as
        // `column::text || '...'`.
        private static readonly string[] EnumTypes =
        [
            "debt_type", "debt_status", "currency_type", "holding_type",
            "market_type", "account_type", "normal_balance_type",
            "transaction_category", "chat_document_kind",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var type in EnumTypes)
                migrationBuilder.Sql($"CREATE CAST ({type} AS text) WITH INOUT AS IMPLICIT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var type in EnumTypes)
                migrationBuilder.Sql($"DROP CAST IF EXISTS ({type} AS text);");
        }
    }
}
