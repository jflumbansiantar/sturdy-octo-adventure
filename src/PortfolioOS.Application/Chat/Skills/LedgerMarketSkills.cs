using MediatR;
using PortfolioOS.Application.Chat.Intents;
using PortfolioOS.Application.Ledger;
using PortfolioOS.Application.Ledger.Queries.GetLedgerSummary;
using PortfolioOS.Application.MarketData.Queries.GetExchangeRate;

namespace PortfolioOS.Application.Chat.Skills;

public sealed class LedgerNetWorthSkill(IMediator mediator) : IChatSkill
{
    public string SkillId => SkillIds.LedgerNetWorth;

    public async Task<ChatAnswer> ExecuteAsync(ChatSkillContext context, CancellationToken ct = default)
    {
        var s = await mediator.Send(new GetLedgerSummaryQuery(), ct);

        var text =
            $"Kekayaan bersih Anda {ChatFormat.Idr(s.NetWorth)} — dari total aset " +
            $"{ChatFormat.Idr(s.TotalAssets)} dikurangi kewajiban {ChatFormat.Idr(s.TotalLiabilities)}. " +
            $"Sepanjang catatan, pendapatan {ChatFormat.Idr(s.TotalIncome)} dan beban " +
            $"{ChatFormat.Idr(s.TotalExpenses)}.";

        var table = new ChatTable(
            ["Pos", "Nilai"],
            [
                ["Total aset", ChatFormat.Idr(s.TotalAssets)],
                ["Total kewajiban", ChatFormat.Idr(s.TotalLiabilities)],
                ["Ekuitas", ChatFormat.Idr(s.TotalEquity)],
                ["Kekayaan bersih", ChatFormat.Idr(s.NetWorth)],
            ]);

        return new ChatAnswer(text, Table: table, Sources: [new ChatSource("Ringkasan buku besar")]);
    }
}

public sealed class LedgerAccountBalanceSkill(IMediator mediator) : IChatSkill
{
    public string SkillId => SkillIds.LedgerAccountBalance;

    public async Task<ChatAnswer> ExecuteAsync(ChatSkillContext context, CancellationToken ct = default)
    {
        var s = await mediator.Send(new GetLedgerSummaryQuery(), ct);
        var match = FindAccount(s.Accounts, context.Question, context.Facts);

        if (match is null)
        {
            var cash = s.Accounts.Where(a => a.Type == "Asset").OrderByDescending(a => a.Balance).Take(5).ToList();
            var table = new ChatTable(
                ["Akun", "Saldo"],
                [.. cash.Select(a => (IReadOnlyList<string>)[$"{a.Code} — {a.Name}", ChatFormat.Idr(a.Balance)])]);

            return new ChatAnswer(
                "Saya belum yakin akun mana yang Anda maksud. Ini saldo akun aset terbesar Anda:",
                Table: table);
        }

        var text =
            $"Saldo akun {match.Code} — {match.Name} adalah {ChatFormat.Idr(match.Balance)}. " +
            $"Terdiri dari saldo awal {ChatFormat.Idr(match.OpeningBalance)}, " +
            $"debit {ChatFormat.Idr(match.TotalDebits)} dan kredit {ChatFormat.Idr(match.TotalCredits)}.";

        return new ChatAnswer(text, Sources: [new ChatSource($"{match.Code} — {match.Name}", match.Id)]);
    }

    private static LedgerAccountDto? FindAccount(
        IReadOnlyList<LedgerAccountDto> accounts, string question, IReadOnlyList<ScoredDocument> facts)
    {
        var byName = accounts.FirstOrDefault(a =>
            question.Contains(a.Name, StringComparison.OrdinalIgnoreCase) ||
            question.Contains(a.Code, StringComparison.OrdinalIgnoreCase));
        if (byName is not null) return byName;

        var card = facts.FirstOrDefault(f => f.Kind == Domain.Enums.ChatDocumentKind.LedgerAccount);
        return card?.SourceId is null ? null : accounts.FirstOrDefault(a => a.Id == card.SourceId);
    }
}

public sealed class MarketFxSkill(IMediator mediator) : IChatSkill
{
    public string SkillId => SkillIds.MarketFx;

    public async Task<ChatAnswer> ExecuteAsync(ChatSkillContext context, CancellationToken ct = default)
    {
        var fx = await mediator.Send(new GetExchangeRateQuery(), ct);

        var freshness = fx.IsLive
            ? $"diambil langsung dari pasar ({fx.AsOf.ToLocalTime():d MMMM yyyy HH:mm})"
            : $"dari cache karena penyedia harga tidak bisa dihubungi ({fx.AsOf.ToLocalTime():d MMMM yyyy HH:mm})";

        var text =
            $"1 {fx.Base} = {ChatFormat.Idr(fx.Rate)}. Kurs ini {freshness}, " +
            $"dan dipakai untuk mengonversi seluruh nilai portofolio ke rupiah.";

        return new ChatAnswer(text, Sources: [new ChatSource($"Kurs {fx.Base}/{fx.Quote}")]);
    }
}

public sealed class HelpCapabilitiesSkill : IChatSkill
{
    public string SkillId => SkillIds.HelpCapabilities;

    public Task<ChatAnswer> ExecuteAsync(ChatSkillContext context, CancellationToken ct = default)
    {
        var table = new ChatTable(
            ["Bisa ditanyakan", "Contoh"],
            [.. IntentCatalog.All
                .Where(i => i.SkillId != SkillIds.HelpCapabilities)
                .Select(i => (IReadOnlyList<string>)[i.Description, i.CanonicalQuestion])]);

        var text =
            "Saya menjawab dari data PortfolioOS Anda sendiri — semua angka diambil langsung dari " +
            "catatan yang sudah tersimpan, bukan diperkirakan. Berikut yang bisa saya jawab:";

        return Task.FromResult(new ChatAnswer(text, Table: table));
    }
}
