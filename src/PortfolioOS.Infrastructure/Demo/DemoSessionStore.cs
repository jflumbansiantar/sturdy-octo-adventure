using Npgsql;

namespace PortfolioOS.Infrastructure.Demo;

/// <summary>One live sandbox, as recorded in the registry.</summary>
public sealed record DemoSessionRecord(
    Guid Id,
    string Schema,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);

/// <summary>
/// The registry of live demo sessions, kept in <c>public.demo_sessions</c>.
/// </summary>
/// <remarks>
/// Deliberately outside the EF model and its migrations. The model is what gets recreated
/// inside every sandbox, and a registry that travelled with it would end up duplicated in each
/// one - a per-session table listing sessions. Raw SQL against a fully qualified
/// <c>public.demo_sessions</c> also makes this the one store that cannot be redirected by a
/// connection's <c>search_path</c>, which matters because it is the authority deciding which
/// schema a request may touch.
/// </remarks>
public sealed class DemoSessionStore(string connectionString)
{
    public async Task EnsureCreatedAsync(CancellationToken ct = default)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS public.demo_sessions (
                id            uuid        PRIMARY KEY,
                schema_name   text        NOT NULL UNIQUE,
                created_at    timestamptz NOT NULL,
                expires_at    timestamptz NOT NULL,
                last_seen_at  timestamptz NOT NULL
            );
            """;

        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task InsertAsync(DemoSessionRecord session, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO public.demo_sessions (id, schema_name, created_at, expires_at, last_seen_at)
            VALUES (@id, @schema, @created, @expires, @created);
            """;

        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", session.Id);
        cmd.Parameters.AddWithValue("schema", session.Schema);
        cmd.Parameters.AddWithValue("created", session.CreatedAt);
        cmd.Parameters.AddWithValue("expires", session.ExpiresAt);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Marks the session as still in use and returns it, or null if it is expired, idle or gone.
    /// </summary>
    /// <remarks>
    /// Validation and the idle-timer reset are one statement on purpose: every demo request
    /// runs this, and splitting it would double the round trips while opening a window where a
    /// session could be purged between the check and the touch.
    /// </remarks>
    public async Task<DemoSessionRecord?> TouchAsync(Guid id, TimeSpan idleTimeout, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE public.demo_sessions
               SET last_seen_at = now()
             WHERE id = @id
               AND expires_at > now()
               AND last_seen_at > now() - @idle
            RETURNING id, schema_name, created_at, expires_at;
            """;

        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("idle", idleTimeout);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Read(reader) : null;
    }

    public async Task<string?> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM public.demo_sessions WHERE id = @id RETURNING schema_name;";

        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        return await cmd.ExecuteScalarAsync(ct) as string;
    }

    /// <summary>Sessions past their lifetime or their idle window, oldest first.</summary>
    public async Task<IReadOnlyList<DemoSessionRecord>> ListStaleAsync(
        TimeSpan idleTimeout, CancellationToken ct = default)
    {
        const string sql = """
            SELECT id, schema_name, created_at, expires_at
              FROM public.demo_sessions
             WHERE expires_at <= now()
                OR last_seen_at <= now() - @idle
             ORDER BY created_at;
            """;

        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("idle", idleTimeout);
        return await ReadAllAsync(cmd, ct);
    }

    public async Task<IReadOnlyList<DemoSessionRecord>> ListAllAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT id, schema_name, created_at, expires_at
              FROM public.demo_sessions
             ORDER BY created_at;
            """;

        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        return await ReadAllAsync(cmd, ct);
    }

    /// <summary>
    /// Every <c>demo_</c>-prefixed schema physically present in the database.
    /// </summary>
    /// <remarks>
    /// The registry can disagree with reality after a crash between creating a schema and
    /// recording it. This is what lets startup find those orphans and drop them.
    /// </remarks>
    public async Task<IReadOnlyList<string>> ListSchemasAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT nspname FROM pg_catalog.pg_namespace WHERE nspname LIKE @prefix;";

        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);

        // The underscore in 'demo_' is a LIKE wildcard; escaped so 'demoX...' cannot match.
        cmd.Parameters.AddWithValue("prefix", @"demo\_%");

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var names = new List<string>();
        while (await reader.ReadAsync(ct)) names.Add(reader.GetString(0));
        return names;
    }

    /// <summary>Removes a schema and everything in it. Safe to call for a name already gone.</summary>
    public async Task DropSchemaAsync(string schema, CancellationToken ct = default)
    {
        if (!DemoSchema.IsValid(schema))
            throw new ArgumentException($"Refusing to drop non-demo schema '{schema}'", nameof(schema));

        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE;", conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task CreateSchemaAsync(string schema, CancellationToken ct = default)
    {
        if (!DemoSchema.IsValid(schema))
            throw new ArgumentException($"Not a demo schema name: '{schema}'", nameof(schema));

        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\";", conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
    {
        var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        return conn;
    }

    private static async Task<IReadOnlyList<DemoSessionRecord>> ReadAllAsync(
        NpgsqlCommand cmd, CancellationToken ct)
    {
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var rows = new List<DemoSessionRecord>();
        while (await reader.ReadAsync(ct)) rows.Add(Read(reader));
        return rows;
    }

    private static DemoSessionRecord Read(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetString(1),
        reader.GetFieldValue<DateTimeOffset>(2),
        reader.GetFieldValue<DateTimeOffset>(3));
}
