using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PortfolioOS.Application.Common.Interfaces;
using PortfolioOS.Infrastructure.Persistence;
using PortfolioOS.Infrastructure.Services.Chat;

namespace PortfolioOS.Infrastructure.Demo;

/// <summary>Thrown when every demo slot is taken. Surfaced to the caller as HTTP 429.</summary>
public sealed class DemoCapacityException(int limit)
    : Exception($"All {limit} demo sessions are in use.")
{
    public int Limit { get; } = limit;
}

/// <summary>
/// Creates, validates and destroys the per-session sandboxes behind the test-drive account.
/// </summary>
/// <remarks>
/// A demo session is one Postgres schema holding a complete, private copy of the application's
/// tables, filled from <see cref="DataSeeder"/>. That choice answers the two hard parts of
/// "let a stranger try the app" at once:
/// <list type="bullet">
/// <item>the tester writes to a real database, so nothing about the app has to be faked or
/// read-only, and no query handler, entity or migration knows demo mode exists;</item>
/// <item>cleanup is <c>DROP SCHEMA CASCADE</c> - one statement that cannot miss a row, and
/// cannot reach the owner's data because it names a schema that only ever held demo data.</item>
/// </list>
/// The obvious alternative - tagging rows with a session id and filtering every query - was
/// rejected: it puts the owner's real holdings one missing <c>WHERE</c> clause away from a
/// stranger's session, and it has no answer for a tester who deletes a seeded row.
/// <para>
/// The seed data is what the tester sees. The owner's own <c>public</c> rows are never copied.
/// </para>
/// </remarks>
public sealed class DemoSessionManager(
    IOptions<DemoOptions> options,
    DemoSessionStore store,
    IDemoConnectionString connectionString,
    IServiceProvider services,
    ILogger<DemoSessionManager> logger)
{
    private readonly DemoOptions _options = options.Value;

    /// <summary>
    /// Provisioning runs DDL and a seed; serialising it keeps a burst of logins from turning
    /// into concurrent schema builds, and makes the capacity check meaningful.
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    public bool IsEnabled => _options.Enabled;

    public DemoOptions Options => _options;

    private TimeSpan IdleTimeout => TimeSpan.FromMinutes(_options.IdleMinutes);

    /// <summary>
    /// Prepares the registry and clears anything left behind by a previous run.
    /// </summary>
    /// <remarks>
    /// Called at startup. A process that dies mid-session leaves a schema with no one to drop
    /// it, so recovery happens here rather than waiting for a janitor tick that assumes a tidy
    /// shutdown.
    /// </remarks>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await store.EnsureCreatedAsync(ct);
        await PurgeStaleAsync(ct);
        await PurgeOrphanSchemasAsync(ct);
    }

    /// <summary>Builds a fresh sandbox and returns the session that owns it.</summary>
    public async Task<DemoSessionRecord> StartAsync(CancellationToken ct = default)
    {
        if (!_options.Enabled)
            throw new InvalidOperationException("Demo mode is disabled.");

        await _gate.WaitAsync(ct);
        try
        {
            // Expired sessions free their slot here rather than only on the janitor's schedule,
            // so a visitor arriving after a quiet hour is not turned away by ghosts.
            await PurgeStaleAsync(ct);

            var live = await store.ListAllAsync(ct);
            if (live.Count >= _options.MaxConcurrentSessions)
                throw new DemoCapacityException(_options.MaxConcurrentSessions);

            var now = DateTimeOffset.UtcNow;
            var session = new DemoSessionRecord(
                Guid.NewGuid(),
                DemoSchema.New(),
                now,
                now.AddMinutes(_options.SessionMinutes));

            await ProvisionAsync(session, ct);
            await store.InsertAsync(session, ct);

            logger.LogInformation(
                "Demo session {SessionId} started in schema {Schema}, expires {ExpiresAt:u}",
                session.Id, session.Schema, session.ExpiresAt);

            WarmChatIndex(session.Schema);
            return session;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Confirms the session is still live and resets its idle timer.
    /// Null means the caller's token outlived its sandbox and should be treated as unauthorised.
    /// </summary>
    public Task<DemoSessionRecord?> TouchAsync(Guid sessionId, CancellationToken ct = default) =>
        store.TouchAsync(sessionId, IdleTimeout, ct);

    /// <summary>
    /// Ends a session and destroys everything it wrote. Idempotent.
    /// </summary>
    /// <returns>True when this call is the one that removed the data.</returns>
    public async Task<bool> EndAsync(Guid sessionId, CancellationToken ct = default)
    {
        var schema = await store.DeleteAsync(sessionId, ct);
        if (schema is null) return false;

        await DestroyAsync(schema, ct);
        logger.LogInformation("Demo session {SessionId} ended; schema {Schema} dropped", sessionId, schema);
        return true;
    }

    /// <summary>Drops the sandboxes of sessions that expired or went quiet.</summary>
    public async Task<int> PurgeStaleAsync(CancellationToken ct = default)
    {
        var stale = await store.ListStaleAsync(IdleTimeout, ct);
        var dropped = 0;

        foreach (var session in stale)
        {
            try
            {
                await store.DeleteAsync(session.Id, ct);
                await DestroyAsync(session.Schema, ct);
                dropped++;
            }
            catch (Exception ex)
            {
                // One stubborn schema must not stop the rest from being cleaned up.
                logger.LogError(ex, "Could not purge demo schema {Schema}", session.Schema);
            }
        }

        if (dropped > 0)
            logger.LogInformation("Purged {Count} stale demo session(s)", dropped);

        return dropped;
    }

    /// <summary>Drops <c>demo_*</c> schemas that no registry row claims.</summary>
    private async Task PurgeOrphanSchemasAsync(CancellationToken ct)
    {
        var known = (await store.ListAllAsync(ct)).Select(s => s.Schema).ToHashSet();
        var present = await store.ListSchemasAsync(ct);

        foreach (var schema in present.Where(s => !known.Contains(s)))
        {
            logger.LogWarning("Dropping orphaned demo schema {Schema}", schema);
            await DestroyAsync(schema, ct);
        }
    }

    private async Task ProvisionAsync(DemoSessionRecord session, CancellationToken ct)
    {
        await store.CreateSchemaAsync(session.Schema, ct);
        try
        {
            await using var db = CreateContext(session.Schema);

            // Migrating rather than copying public's structure: the migrations are the model's
            // one description of itself, so the sandbox gains every constraint, index and
            // future schema change for free. The DDL is unqualified and the connection's
            // search_path points at the sandbox, so all of it lands there.
            await db.Database.MigrateAsync(ct);
            await DataSeeder.SeedAsync(db);
        }
        catch
        {
            // Never leave a half-built schema behind to be counted against the session cap.
            await store.DropSchemaAsync(session.Schema, CancellationToken.None);
            throw;
        }
    }

    private async Task DestroyAsync(string schema, CancellationToken ct)
    {
        await store.DropSchemaAsync(schema, ct);

        // Npgsql pools per connection string, so the sandbox's pool would otherwise keep idle
        // connections open against a schema that no longer exists.
        Npgsql.NpgsqlConnection.ClearPool(
            new Npgsql.NpgsqlConnection(DemoSchema.ConnectionStringFor(connectionString.Value, schema)));
    }

    /// <summary>
    /// Embeds the sandbox's chat corpus in the background.
    /// </summary>
    /// <remarks>
    /// Off the login path deliberately: the first embedding of a process loads a ~490MB ONNX
    /// model and costs about five seconds, which would read as a broken login button. The
    /// assistant is the only feature that waits for this, and a tester needs longer than that
    /// to reach its page. Chat degrades to "no answer" if it never finishes - the model is an
    /// optional download - which is exactly how the rest of the app treats it.
    /// </remarks>
    private void WarmChatIndex(string schema)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await using var db = CreateContext(schema);
                var indexer = new ChatIndexService(
                    db,
                    services.GetRequiredService<IEmbeddingService>(),
                    services.GetRequiredService<ILogger<ChatIndexService>>());

                var result = await indexer.ReindexAsync();
                logger.LogInformation(
                    "Demo chat index built for {Schema}: {Embedded} embedded", schema, result.Embedded);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Demo chat index unavailable for {Schema}", schema);
            }
        });
    }

    private ApplicationDbContext CreateContext(string schema)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                DemoSchema.ConnectionStringFor(connectionString.Value, schema),
                npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);

                    // Naming the schema explicitly is what makes a sandbox actually get built.
                    // Left to its default the history table is looked up by visibility, and
                    // public's copy is visible on this connection - so EF would read the
                    // owner's migration history, conclude the sandbox was already up to date,
                    // and create no tables at all.
                    npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, schema);

                    npgsql.UseVector();
                })
            .Options;

        return new ApplicationDbContext(options);
    }
}

/// <summary>The application's own connection string, before any sandbox redirects it.</summary>
public interface IDemoConnectionString
{
    string Value { get; }
}

internal sealed class DemoConnectionString(string value) : IDemoConnectionString
{
    public string Value { get; } = value;
}
