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
    ILogger<ChatIndexBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let migrations and seeding finish before the first sweep reads the tables.
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = services.CreateScope();
                var indexer = scope.ServiceProvider.GetRequiredService<IChatIndexService>();
                await indexer.ReindexAsync(stoppingToken);
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
