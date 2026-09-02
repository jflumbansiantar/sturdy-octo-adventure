using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace PortfolioOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChatDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            // Same native-enum treatment the other enums get in InitialCreate: the type has to
            // exist before the table that declares a column of it, and Npgsql sends the
            // string-converted value as `text`, which Postgres will not implicitly cast.
            migrationBuilder.Sql(
                "CREATE TYPE chat_document_kind AS ENUM " +
                "('IntentPhrase', 'Holding', 'Debt', 'Transaction', 'JournalEntry', 'LedgerAccount', 'HelpTopic');");
            migrationBuilder.Sql("CREATE CAST (text AS chat_document_kind) WITH INOUT AS ASSIGNMENT;");

            migrationBuilder.CreateTable(
                name: "chat_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    kind = table.Column<string>(type: "chat_document_kind", nullable: false),
                    source_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    skill_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    content = table.Column<string>(type: "text", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    embedding = table.Column<Vector>(type: "vector(384)", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_documents", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_chat_documents_kind",
                table: "chat_documents",
                column: "kind");

            migrationBuilder.CreateIndex(
                name: "idx_chat_documents_source",
                table: "chat_documents",
                columns: new[] { "kind", "source_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chat_documents");

            migrationBuilder.Sql("DROP CAST IF EXISTS (text AS chat_document_kind);");
            migrationBuilder.Sql("DROP TYPE IF EXISTS chat_document_kind;");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
