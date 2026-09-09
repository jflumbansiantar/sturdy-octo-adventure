using MediatR;
using PortfolioOS.Application.Chat.Intents;
using PortfolioOS.Application.Chat.Slots;
using PortfolioOS.Application.Transactions.Queries.GetTransactions;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Application.Chat.Skills;

public sealed class SpendInPeriodSkill(IMediator mediator) : IChatSkill
{
    public string SkillId => SkillIds.TransactionsSpendInPeriod;

    public async Task<ChatAnswer> ExecuteAsync(ChatSkillContext context, CancellationToken ct = default)
    {
        // No period named means the whole history, not "today" - guessing a narrower range than
        // the user asked for would quietly under-report their spending.
        var period = RelativePeriodParser.Parse(context.Question, context.Today);
        var label = period?.Label ?? "sepanjang catatan";

        var rows = await mediator.Send(
            new GetTransactionsQuery(TransactionCategory.Expense, period?.From, period?.To), ct);

        if (rows.Count == 0)
            return new ChatAnswer($"Tidak ada pengeluaran yang tercatat untuk {label}.");

        var total = rows.Sum(r => r.Total);
        var biggest = rows.OrderByDescending(r => r.Total).First();

        var text =
            $"Total pengeluaran {label} adalah {ChatFormat.Idr(total)} dari {rows.Count} transaksi. " +
            $"Yang terbesar {ChatFormat.Idr(biggest.Total)} untuk \"{biggest.Name}\" " +
            $"pada {ChatFormat.Date(biggest.Date)}.";

        var table = new ChatTable(
            ["Tanggal", "Keterangan", "Jenis", "Jumlah"],
            [.. rows.OrderByDescending(r => r.Total).Take(10).Select(r => (IReadOnlyList<string>)
                [ChatFormat.Date(r.Date), r.Name, r.Type, ChatFormat.Idr(r.Total)])]);

        return new ChatAnswer(text, Table: table, Sources: [new ChatSource($"Transaksi {label}")]);
    }
}

public sealed class RecentTransactionsSkill(IMediator mediator) : IChatSkill
{
    public string SkillId => SkillIds.TransactionsRecent;

    public async Task<ChatAnswer> ExecuteAsync(ChatSkillContext context, CancellationToken ct = default)
    {
        var rows = await mediator.Send(new GetTransactionsQuery(), ct);

        if (rows.Count == 0)
            return new ChatAnswer("Belum ada transaksi yang tercatat.");

        var recent = rows.OrderByDescending(r => r.Date).ThenByDescending(r => r.CreatedAt).Take(10).ToList();
        var newest = recent[0];

        var text =
            $"Transaksi terakhir Anda: {newest.Type} \"{newest.Name}\" senilai " +
            $"{ChatFormat.Idr(newest.Total)} pada {ChatFormat.Date(newest.Date)}. " +
            $"Berikut {recent.Count} yang terbaru.";

        var table = new ChatTable(
            ["Tanggal", "Kategori", "Keterangan", "Jumlah"],
            [.. recent.Select(r => (IReadOnlyList<string>)
                [ChatFormat.Date(r.Date), r.Category, r.Name, ChatFormat.Idr(r.Total)])]);

        return new ChatAnswer(text, Table: table,
            Sources: [.. recent.Take(3).Select(r => new ChatSource(r.Name, r.Id.ToString()))]);
    }
}

public sealed class TransactionsByCategorySkill(IMediator mediator) : IChatSkill
{
    public string SkillId => SkillIds.TransactionsByCategory;

    public async Task<ChatAnswer> ExecuteAsync(ChatSkillContext context, CancellationToken ct = default)
    {
        var period = RelativePeriodParser.Parse(context.Question, context.Today);
        var label = period?.Label ?? "sepanjang catatan";

        var rows = await mediator.Send(new GetTransactionsQuery(null, period?.From, period?.To), ct);

        if (rows.Count == 0)
            return new ChatAnswer($"Tidak ada transaksi yang tercatat untuk {label}.");

        // Grouped by the free-text Type ("FOOD", "SALARY", ...) rather than the four coarse
        // categories, because that is the level users actually think in.
        var expenses = rows.Where(r => r.Category == nameof(TransactionCategory.Expense)).ToList();
        var groups = expenses
            .GroupBy(r => string.IsNullOrWhiteSpace(r.Type) ? "Lainnya" : r.Type)
            .Select(g => (Label: g.Key, Total: g.Sum(r => r.Total), Count: g.Count()))
            .OrderByDescending(g => g.Total)
            .ToList();

        var income = rows.Where(r => r.Category == nameof(TransactionCategory.Income)).Sum(r => r.Total);
        var spent = expenses.Sum(r => r.Total);

        var text = groups.Count == 0
            ? $"Untuk {label} tercatat pemasukan {ChatFormat.Idr(income)} dan belum ada pengeluaran."
            : $"Untuk {label}, pengeluaran terbesar Anda ada di \"{groups[0].Label}\" " +
              $"sebesar {ChatFormat.Idr(groups[0].Total)} dari {groups[0].Count} transaksi. " +
              $"Total keluar {ChatFormat.Idr(spent)}, total masuk {ChatFormat.Idr(income)}, " +
              $"selisih {ChatFormat.SignedIdr(income - spent)}.";

        var table = new ChatTable(
            ["Jenis", "Jumlah transaksi", "Total"],
            [.. groups.Select(g => (IReadOnlyList<string>)
                [g.Label, g.Count.ToString(), ChatFormat.Idr(g.Total)])]);

        return new ChatAnswer(text, Table: table, Sources: [new ChatSource($"Transaksi {label}")]);
    }
}
