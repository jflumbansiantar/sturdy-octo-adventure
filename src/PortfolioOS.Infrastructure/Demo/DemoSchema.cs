using System.Text.RegularExpressions;
using Npgsql;

namespace PortfolioOS.Infrastructure.Demo;

/// <summary>
/// Names and validates the throwaway Postgres schema that backs one demo session.
/// </summary>
public static partial class DemoSchema
{
    public const string Prefix = "demo_";

    /// <summary>Matches the names <see cref="New"/> produces and nothing else.</summary>
    [GeneratedRegex("^demo_[0-9a-f]{12}$")]
    private static partial Regex NamePattern();

    public static string New() => Prefix + Guid.NewGuid().ToString("n")[..12];

    /// <summary>
    /// Whether a name is one of ours and safe to interpolate into DDL.
    /// </summary>
    /// <remarks>
    /// Schema names cannot be parameterised in <c>CREATE SCHEMA</c> / <c>DROP SCHEMA</c>, so
    /// every path that builds that SQL checks here first. The names only ever arrive from our
    /// own registry table, but a whitelist is cheap and a schema name reaching DDL unchecked is
    /// the one place in this feature where an injection could hide.
    /// </remarks>
    public static bool IsValid(string? name) => name is not null && NamePattern().IsMatch(name);

    /// <summary>
    /// The application's connection string, pointed at one demo schema.
    /// </summary>
    /// <remarks>
    /// This single line is what isolates a demo session: every unqualified table name in the
    /// app - EF-generated or the chat retriever's hand-written SQL - resolves against
    /// <paramref name="schema"/> instead of <c>public</c>, so no query handler, entity or
    /// migration had to learn that demo mode exists.
    /// <para>
    /// <c>public</c> stays on the path behind it because the <c>vector</c> type and pgvector's
    /// operators live there. It is a fallback for types, not for tables: the schema is created
    /// with a full set of tables, and a session whose schema has been dropped is rejected at
    /// the middleware before any query runs.
    /// </para>
    /// </remarks>
    public static string ConnectionStringFor(string baseConnectionString, string schema)
    {
        if (!IsValid(schema))
            throw new ArgumentException($"Not a demo schema name: '{schema}'", nameof(schema));

        return new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            SearchPath = $"{schema},public",

            // Npgsql pools per connection string, so every live sandbox gets a pool of its own.
            // Left at the default 100 each, a handful of testers could exhaust Postgres's
            // connection limit between them; a demo browser needs a fraction of this.
            MaxPoolSize = 10
        }.ConnectionString;
    }
}
