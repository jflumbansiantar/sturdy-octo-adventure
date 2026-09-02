using System.Diagnostics;
using PortfolioOS.Application.Chat;
using PortfolioOS.Application.Common.Interfaces;

namespace PortfolioOS.API.Services;

/// <summary>
/// Keeps the chat index fresh without anyone having to remember to rebuild it.
/// </summary>
/// <remarks>
/// A periodic sweep rather than write-time hooks: content hashing makes an unchanged rebuild
/// almost free, so a timer buys freshness for every write path — including rows inserted
/// outside the API — without threading an invalidation call through every command handler.
/// <para>
/// Failure here must never take the API down. The embedding model may legitimately be absent
/// (it is a ~490MB optional download), in which case chat is unavailable and everything else
/// carries on.
/// </para>
/// </remarks>
public class ChatIndexBackgroundService(
    IServiceProvider services,
    IEmbeddingService embedder,
    ILogger<ChatIndexBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Program.cs runs migrations and seeding between Build() and Run(), and hosted services
        // only start inside Run() - so the tables are ready by now. This short pause just keeps
        // the sweep off the critical path while the host finishes wiring up.
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        var warmed = false;

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = services.CreateScope();
                var indexer = scope.ServiceProvider.GetRequiredService<IChatIndexService>();
                var result = await indexer.ReindexAsync(stoppingToken);

                // Warm the model if the rebuild did not already force it open.
                //
                // Content hashing and lazy loading are each worth having, but together they
                // hand the user the bill: when nothing has changed the sweep embeds nothing, so
                // the ONNX session stays unloaded and the FIRST question of the session pays
                // the full ~5 second load. Measured: 5.4s for that question, 0.05-0.14s for
                // every one after it. One throwaway embedding here moves that cost off the
                // user's first question.
                if (result.Embedded == 0 && !warmed)
                {
                    var sw = Stopwatch.StartNew();
                    await embedder.EmbedAsync("warmup", EmbeddingKind.Query, stoppingToken);
                    logger.LogInformation("Embedding model warmed in {Elapsed}ms", sw.ElapsedMilliseconds);
                }

                warmed = true;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (FileNotFoundException ex)
            {
                logger.LogWarning(
                    "Chat index skipped: embedding model not available ({Message}). " +
                    "Run scripts/fetch-embedding-model.sh to enable the chat feature.", ex.Message);
                break;      // no point retrying every 15 minutes for a missing file
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Chat index rebuild failed; will retry at the next interval");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
