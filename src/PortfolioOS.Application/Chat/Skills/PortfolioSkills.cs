using MediatR;
using PortfolioOS.Application.Chat.Intents;
using PortfolioOS.Application.Holdings;
using PortfolioOS.Application.Holdings.Queries.GetHoldings;
using PortfolioOS.Application.Portfolio.Queries.GetPortfolioSummary;

namespace PortfolioOS.Application.Chat.Skills;

public sealed class PortfolioSummarySkill(IMediator mediator) : IChatSkill
{
    public string SkillId => SkillIds.PortfolioSummary;

    public async Task<ChatAnswer> ExecuteAsync(ChatSkillContext context, CancellationToken ct = default)
    {
        var s = await mediator.Send(new GetPortfolioSummaryQuery(), ct);

        if (s.HoldingCount == 0)
            return new ChatAnswer("Belum ada holding yang tercatat, jadi nilai portofolio masih kosong.");

        var verdict = s.TotalGainLoss >= 0 ? "untung" : "rugi";

        // A stale FX rate silently changes every converted total, so say so rather than
        // presenting an approximation as fact.
        var caveat = s.IsRateLive
            ? ""
            : $" (kurs USD/IDR sedang tidak live, memakai {ChatFormat.Idr(s.UsdIdrRate)} dari cache, jadi angkanya perkiraan)";

        var text =
            $"Total nilai portofolio Anda {ChatFormat.Idr(s.TotalValue)} dari {s.HoldingCount} holding. " +
            $"Modal yang ditanam {ChatFormat.Idr(s.TotalCostBasis)}, jadi posisi Anda {verdict} " +
            $"{ChatFormat.SignedIdr(s.TotalGainLoss)} ({ChatFormat.Pct(s.TotalGainLossPct)}). " +
            $"Hari ini bergerak {ChatFormat.SignedIdr(s.TodayGainLoss)}.{caveat}";

        return new ChatAnswer(text, Sources: [new ChatSource("Ringkasan portofolio")]);
    }
}

public sealed class PortfolioTopMoversSkill(IMediator mediator) : IChatSkill
{
    public string SkillId => SkillIds.PortfolioTopMovers;

    public async Task<ChatAnswer> ExecuteAsync(ChatSkillContext context, CancellationToken ct = default)
    {
        var holdings = await mediator.Send(new GetHoldingsQuery(), ct);
        var priced = holdings.Where(h => h.CurrentPrice > 0).ToList();

        if (priced.Count == 0)
            return new ChatAnswer("Belum ada harga terbaru untuk holding Anda, jadi pergerakan hari ini belum bisa dihitung.");

        var ranked = priced.OrderByDescending(h => h.DayChangePct).ToList();
        var best = ranked[0];
        var worst = ranked[^1];

        var text =
            $"Hari ini {best.Ticker} bergerak paling positif ({ChatFormat.Pct(best.DayChangePct)}, " +
            $"{ChatFormat.SignedIdr(best.DayGainLoss)}), sedangkan {worst.Ticker} paling negatif " +
            $"({ChatFormat.Pct(worst.DayChangePct)}, {ChatFormat.SignedIdr(worst.DayGainLoss)}).";

        var table = new ChatTable(
            ["Ticker", "Perubahan", "Dampak"],
            [.. ranked.Take(5).Select(h => (IReadOnlyList<string>)
                [h.Ticker, ChatFormat.Pct(h.DayChangePct), ChatFormat.SignedIdr(h.DayGainLoss)])]);

        return new ChatAnswer(text, Table: table,
            Sources: [.. ranked.Take(5).Select(h => new ChatSource(h.Ticker, h.Id.ToString()))]);
    }
}

public sealed class PortfolioAllocationSkill(IMediator mediator) : IChatSkill
{
    public string SkillId => SkillIds.PortfolioAllocation;

    public async Task<ChatAnswer> ExecuteAsync(ChatSkillContext context, CancellationToken ct = default)
    {
        var holdings = await mediator.Send(new GetHoldingsQuery(), ct);
        var total = holdings.Sum(h => h.MarketValue);

        if (total <= 0)
            return new ChatAnswer("Nilai portofolio masih nol, jadi komposisinya belum bisa dihitung.");

        var byType = holdings
            .GroupBy(h => h.Type)
            .Select(g => (Label: g.Key, Value: g.Sum(h => h.MarketValue)))
            .OrderByDescending(x => x.Value)
            .ToList();

        var byMarket = holdings
            .GroupBy(h => h.Market)
            .Select(g => (Label: g.Key, Value: g.Sum(h => h.MarketValue)))
            .OrderByDescending(x => x.Value)
            .ToList();

        var biggest = byType[0];
        var text =
            $"Dari total {ChatFormat.Idr(total)}, porsi terbesar ada di {biggest.Label} " +
            $"({ChatFormat.Rate(biggest.Value / total * 100)} dari portofolio). " +
            $"Per pasar: {string.Join(", ", byMarket.Select(m => $"{m.Label} {ChatFormat.Rate(m.Value / total * 100)}"))}.";

        var table = new ChatTable(
            ["Kategori", "Nilai", "Porsi"],
            [
                .. byType.Select(x => (IReadOnlyList<string>)
                    [x.Label, ChatFormat.Idr(x.Value), ChatFormat.Rate(x.Value / total * 100)]),
                .. byMarket.Select(x => (IReadOnlyList<string>)
                    [$"Pasar {x.Label}", ChatFormat.Idr(x.Value), ChatFormat.Rate(x.Value / total * 100)]),
            ]);

        return new ChatAnswer(text, Table: table, Sources: [new ChatSource("Komposisi portofolio")]);
    }
}

public sealed class HoldingDetailSkill(IMediator mediator) : IChatSkill
{
    public string SkillId => SkillIds.HoldingDetail;

    public async Task<ChatAnswer> ExecuteAsync(ChatSkillContext context, CancellationToken ct = default)
    {
        var holdings = await mediator.Send(new GetHoldingsQuery(), ct);
        var match = HoldingMatcher.Find(holdings, context.Question, context.Facts);

        if (match is null)
        {
            var known = string.Join(", ", holdings.Take(8).Select(h => h.Ticker));
            return new ChatAnswer(
                $"Saya belum bisa memastikan saham mana yang Anda maksud. Sebutkan tickernya, misalnya: {known}.",
                Suggestions: [.. holdings.Take(3).Select(h => $"Bagaimana posisi {h.Ticker} saya?")]);
        }

        // Per-unit prices stay in the currency of their own exchange; totals are already IDR.
        var text =
            $"{match.Ticker} ({match.Name}): Anda memegang {ChatFormat.Units(match.Shares)} unit " +
            $"dengan harga rata-rata {ChatFormat.Money(match.AvgCost, match.PriceCurrency)} per unit. " +
            $"Harga terakhir {ChatFormat.Money(match.CurrentPrice, match.PriceCurrency)}, " +
            $"nilai posisi {ChatFormat.Idr(match.MarketValue)} " +
            $"({ChatFormat.SignedIdr(match.GainLoss)}, {ChatFormat.Pct(match.GainLossPct)}). " +
            $"Hari ini {ChatFormat.Pct(match.DayChangePct)}.";

        return new ChatAnswer(text, Sources: [new ChatSource(match.Ticker, match.Id.ToString())]);
    }
}

/// <summary>
/// Works out which holding a question is about.
/// </summary>
/// <remarks>
/// Literal matching on purpose. Embeddings are poor at short opaque tokens like "BBCA" — they
/// carry almost no semantic content — so the ticker is found by exact word match first, then by
/// company name, and only then by falling back to whichever holding fact card the vector search
/// ranked highest.
/// </remarks>
internal static class HoldingMatcher
{
    public static HoldingDto? Find(
        IReadOnlyList<HoldingDto> holdings, string question, IReadOnlyList<ScoredDocument> facts)
    {
        var words = question
            .Split([' ', ',', '.', '?', '!', ':', ';', '(', ')', '\'', '"'], StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim().ToUpperInvariant())
            .ToHashSet();

        var byTicker = holdings.FirstOrDefault(h => words.Contains(h.Ticker.ToUpperInvariant()));
        if (byTicker is not null) return byTicker;

        var byName = holdings.FirstOrDefault(h =>
            !string.IsNullOrWhiteSpace(h.Name) &&
            question.Contains(h.Name, StringComparison.OrdinalIgnoreCase));
        if (byName is not null) return byName;

        var topCard = facts.FirstOrDefault(f => f.Kind == Domain.Enums.ChatDocumentKind.Holding);
        return topCard?.SourceId is not null && Guid.TryParse(topCard.SourceId, out var id)
            ? holdings.FirstOrDefault(h => h.Id == id)
            : null;
    }
}
