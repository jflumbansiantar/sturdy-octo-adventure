using System.Text.Json;
using PortfolioOS.Application.Common.Interfaces;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Infrastructure.Services;

public class YahooFinanceMarketDataService(HttpClient http) : IMarketDataService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<QuoteResult>> GetQuotesAsync(
        IEnumerable<QuoteRequest> requests, CancellationToken ct = default)
    {
        var results = new List<QuoteResult>();

        foreach (var req in requests)
        {
            var yahooTicker = ToYahooTicker(req);
            if (yahooTicker is null) continue;   // not quotable on Yahoo

            try
            {
                var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(yahooTicker)}?interval=1d&range=1d";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", "Mozilla/5.0");

                using var response = await http.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode) continue;

                var json = await response.Content.ReadAsStringAsync(ct);
                var quote = ParseQuote(req.Ticker, json);
                if (quote is not null)
                    results.Add(quote);
            }
            catch
            {
                // skip tickers that fail; caller handles missing data
            }
        }

        return results;
    }

    /// <summary>
    /// Yahoo quotes FX pairs under a "=X" symbol, and "IDR=X" is the USD-IDR pair rather
    /// than anything IDR-based - the base currency is implied to be USD.
    /// </summary>
    private const string UsdIdrSymbol = "IDR=X";

    public async Task<decimal?> GetUsdIdrRateAsync(CancellationToken ct = default)
    {
        try
        {
            var json = await FetchChartAsync(UsdIdrSymbol, ct);
            if (json is null) return null;

            var quote = ParseQuote(UsdIdrSymbol, json);

            // A rate near 1 means the response was not the pair we asked for; using it would
            // wipe out the dollar side of the portfolio, so treat it as a failed lookup.
            return quote is { CurrentPrice: > 100m } ? quote.CurrentPrice : null;
        }
        catch
        {
            return null;   // offline or throttled - the caller falls back to a cached rate
        }
    }

    /// <summary>Fetches one symbol from the chart endpoint. Null when Yahoo declines.</summary>
    private async Task<string?> FetchChartAsync(string yahooSymbol, CancellationToken ct)
    {
        var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(yahooSymbol)}?interval=1d&range=1d";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", "Mozilla/5.0");

        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;

        return await response.Content.ReadAsStringAsync(ct);
    }

    /// <summary>
    /// Maps a holding to the symbol Yahoo actually uses.
    /// Indonesian equities need the ".JK" (Jakarta) suffix and crypto needs "-USD";
    /// without them Yahoo resolves a different instrument, or none at all.
    /// Returns null for instruments Yahoo does not quote.
    /// </summary>
    private static string? ToYahooTicker(QuoteRequest req)
    {
        var ticker = req.Ticker.Trim().ToUpperInvariant();

        // already carries an explicit suffix - trust the caller
        if (ticker.Contains('.') || ticker.Contains('-')) return ticker;

        return (req.Type, req.Market) switch
        {
            (HoldingType.Crypto, _)     => $"{ticker}-USD",
            (HoldingType.MutualFund, _) => null,          // Indonesian funds aren't listed on Yahoo
            (_, Market.ID)              => $"{ticker}.JK",
            _                           => ticker
        };
    }

    private static QuoteResult? ParseQuote(string originalTicker, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("chart", out var chart)) return null;
        if (!chart.TryGetProperty("result", out var resultArr)) return null;
        if (resultArr.ValueKind != JsonValueKind.Array || resultArr.GetArrayLength() == 0) return null;

        var result = resultArr[0];
        if (!result.TryGetProperty("meta", out var meta)) return null;

        var currentPrice = meta.TryGetProperty("regularMarketPrice", out var p) ? p.GetDecimal() : 0m;
        var previousClose = meta.TryGetProperty("previousClose", out var pc)
            ? pc.GetDecimal()
            : meta.TryGetProperty("chartPreviousClose", out var cpc) ? cpc.GetDecimal() : 0m;
        var currency = meta.TryGetProperty("currency", out var cur) ? cur.GetString() ?? "USD" : "USD";

        if (currentPrice == 0m) return null;

        return new QuoteResult(originalTicker, currentPrice, previousClose, currency);
    }
}
