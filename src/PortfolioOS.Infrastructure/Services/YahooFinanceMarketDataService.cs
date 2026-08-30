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
