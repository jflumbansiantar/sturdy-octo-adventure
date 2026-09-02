using PortfolioOS.Application.Chat.Intents;

namespace PortfolioOS.Application.Chat.Slots;

/// <summary>
/// Finds skills a question supports through plain string matching rather than embeddings.
/// </summary>
/// <remarks>
/// This is the "hybrid" half of retrieval. Cosine similarity is weak exactly where these signals
/// are strong: a bare ticker carries almost no semantic content, and "april 2026" looks much like
/// any other question about spending.
/// <para>
/// A signal never answers on its own — it only lowers the bar for a skill the embedding already
/// ranked first (see <see cref="IntentRouter.Route"/>). That is what keeps "tolong belikan saya
/// saham BBCA" refused despite naming a real ticker.
/// </para>
/// <para>
/// Pulled out of the handler so the routing evaluation exercises the same code the API does,
/// rather than a copy of it that can drift.
/// </para>
/// </remarks>
public static class LiteralSignalDetector
{
    private static readonly char[] Separators =
        [' ', ',', '.', '?', '!', ':', ';', '(', ')', '\'', '"'];

    public static IReadOnlyCollection<string> Detect(
        string question, DateOnly today, IEnumerable<string> tickers)
    {
        var signals = new HashSet<string>(StringComparer.Ordinal);

        if (RelativePeriodParser.Parse(question, today) is not null)
        {
            signals.Add(SkillIds.TransactionsSpendInPeriod);
            signals.Add(SkillIds.TransactionsByCategory);
        }

        var words = question
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToUpperInvariant())
            .ToHashSet(StringComparer.Ordinal);

        if (tickers.Any(t => words.Contains(t.ToUpperInvariant())))
            signals.Add(SkillIds.HoldingDetail);

        return signals;
    }

    /// <summary>
    /// Reads the ticker off a holding fact card, which is written as "TICKER — Name. ...".
    /// </summary>
    public static string TickerOf(string factCardContent)
    {
        var end = factCardContent.IndexOf(' ');
        return end < 0 ? factCardContent : factCardContent[..end];
    }
}
