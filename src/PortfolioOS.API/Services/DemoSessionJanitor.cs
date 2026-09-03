using PortfolioOS.Infrastructure.Demo;

namespace PortfolioOS.API.Services;

/// <summary>
/// Deletes the sandboxes of demo sessions that ended without anyone pressing logout.
/// </summary>
/// <remarks>
/// This, not the logout button, is what actually keeps the promise that test data disappears.
/// Testers close the tab, lose the connection, or wander off; a browser-side "log me out on
/// unload" hook cannot be relied on either, and would end the session on an ordinary page
/// refresh. So the server times sessions out on its own and this sweep collects them.
/// </remarks>
public class DemoSessionJanitor(
    DemoSessionManager sessions,
    ILogger<DemoSessionJanitor> logger) : BackgroundService
{
    /// <summary>
    /// Well below the shortest idle window, so an abandoned sandbox is measured in minutes
    /// rather than left until the next tick happens to come round.
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!sessions.IsEnabled) return;

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await sessions.PurgeStaleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A failed sweep is retried on the next tick; it must never take the API down.
                logger.LogError(ex, "Demo session sweep failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
