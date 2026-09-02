using MediatR;
using PortfolioOS.Application.Chat.Intents;
using PortfolioOS.Application.Debts;
using PortfolioOS.Application.Debts.Queries.GetDebts;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Application.Chat.Skills;

public sealed class DebtsTotalOutstandingSkill(IMediator mediator) : IChatSkill
{
    public string SkillId => SkillIds.DebtsTotalOutstanding;

    public async Task<ChatAnswer> ExecuteAsync(ChatSkillContext context, CancellationToken ct = default)
    {
        var debts = await mediator.Send(new GetDebtsQuery(), ct);
        var active = debts.Where(d => d.Status != nameof(DebtStatus.Lunas)).ToList();

        if (active.Count == 0)
            return new ChatAnswer("Tidak ada utang aktif — semuanya sudah lunas.");

        // Balances are per-debt in their own currency, so they are reported per row rather than
        // summed into a single misleading figure. Only the rupiah debts are totalled.
        var idr = active.Where(d => d.Currency == nameof(CurrencyType.IDR)).ToList();
        var usd = active.Where(d => d.Currency == nameof(CurrencyType.USD)).ToList();

        var parts = new List<string>();
        if (idr.Count > 0) parts.Add(ChatFormat.Idr(idr.Sum(d => d.Balance)));
        if (usd.Count > 0) parts.Add(ChatFormat.Money(usd.Sum(d => d.Balance), "USD"));

        var minimum = idr.Sum(d => d.MinimumPayment);
        var text =
            $"Anda punya {active.Count} utang aktif dengan sisa {string.Join(" dan ", parts)}. " +
            $"Total pembayaran minimum per bulan {ChatFormat.Idr(minimum)}.";

        var table = new ChatTable(
            ["Utang", "Sisa", "Bunga/tahun", "Min. bayar", "Jatuh tempo"],
            [.. active.OrderByDescending(d => d.Balance).Select(d => (IReadOnlyList<string>)
                [d.Name, ChatFormat.Money(d.Balance, d.Currency), ChatFormat.Rate(d.InterestRate),
                 ChatFormat.Money(d.MinimumPayment, d.Currency), $"tgl {d.DueDay}"])]);

        return new ChatAnswer(text, Table: table,
            Sources: [.. active.Select(d => new ChatSource(d.Name, d.Id.ToString()))]);
    }
}

public sealed class DebtsHighestInterestSkill(IMediator mediator) : IChatSkill
{
    public string SkillId => SkillIds.DebtsHighestInterest;

    public async Task<ChatAnswer> ExecuteAsync(ChatSkillContext context, CancellationToken ct = default)
    {
        var debts = await mediator.Send(new GetDebtsQuery(), ct);
        var active = debts.Where(d => d.Status != nameof(DebtStatus.Lunas)).ToList();

        if (active.Count == 0)
            return new ChatAnswer("Tidak ada utang aktif, jadi tidak ada bunga yang perlu dikhawatirkan.");

        var ranked = active.OrderByDescending(d => d.InterestRate).ToList();
        var worst = ranked[0];

        var text =
            $"Bunga tertinggi ada di \"{worst.Name}\" — {ChatFormat.Rate(worst.InterestRate)} per tahun " +
            $"atas sisa {ChatFormat.Money(worst.Balance, worst.Currency)}. " +
            (ranked.Count > 1
                ? $"Sebagai pembanding, yang terendah \"{ranked[^1].Name}\" di {ChatFormat.Rate(ranked[^1].InterestRate)}."
                : "");

        var table = new ChatTable(
            ["Utang", "Bunga/tahun", "Sisa", "Jenis"],
            [.. ranked.Select(d => (IReadOnlyList<string>)
                [d.Name, ChatFormat.Rate(d.InterestRate), ChatFormat.Money(d.Balance, d.Currency), d.Type])]);

        return new ChatAnswer(text, Table: table,
            Sources: [.. ranked.Take(3).Select(d => new ChatSource(d.Name, d.Id.ToString()))]);
    }
}

public sealed class DebtsDueSoonSkill(IMediator mediator) : IChatSkill
{
    public string SkillId => SkillIds.DebtsDueSoon;

    public async Task<ChatAnswer> ExecuteAsync(ChatSkillContext context, CancellationToken ct = default)
    {
        var debts = await mediator.Send(new GetDebtsQuery(), ct);
        var active = debts.Where(d => d.Status != nameof(DebtStatus.Lunas)).ToList();

        if (active.Count == 0)
            return new ChatAnswer("Tidak ada tagihan yang menunggu — semua utang sudah lunas.");

        // DueDay is a day-of-month, so "soonest" means the next occurrence of that day,
        // rolling into next month once it has passed.
        var ordered = active
            .Select(d => (Debt: d, InDays: DaysUntil(d.DueDay, context.Today)))
            .OrderBy(x => x.InDays)
            .ToList();

        var next = ordered[0];
        var whenText = next.InDays == 0 ? "hari ini" : $"{next.InDays} hari lagi";

        var text =
            $"Tagihan terdekat adalah \"{next.Debt.Name}\", jatuh tempo tanggal {next.Debt.DueDay} " +
            $"({whenText}), minimum {ChatFormat.Money(next.Debt.MinimumPayment, next.Debt.Currency)}.";

        var table = new ChatTable(
            ["Utang", "Tanggal", "Dalam", "Min. bayar"],
            [.. ordered.Select(x => (IReadOnlyList<string>)
                [x.Debt.Name, $"tgl {x.Debt.DueDay}", $"{x.InDays} hari",
                 ChatFormat.Money(x.Debt.MinimumPayment, x.Debt.Currency)])]);

        return new ChatAnswer(text, Table: table,
            Sources: [.. ordered.Take(3).Select(x => new ChatSource(x.Debt.Name, x.Debt.Id.ToString()))]);
    }

    /// <summary>Days from <paramref name="today"/> to the next occurrence of <paramref name="dueDay"/>.</summary>
    public static int DaysUntil(int dueDay, DateOnly today)
    {
        // Clamp: a due day of 31 in a 30-day month falls on the last day of that month.
        var thisMonth = new DateOnly(today.Year, today.Month,
            Math.Min(dueDay, DateTime.DaysInMonth(today.Year, today.Month)));

        if (thisMonth >= today) return thisMonth.DayNumber - today.DayNumber;

        var next = today.AddMonths(1);
        var nextMonth = new DateOnly(next.Year, next.Month,
            Math.Min(dueDay, DateTime.DaysInMonth(next.Year, next.Month)));

        return nextMonth.DayNumber - today.DayNumber;
    }
}
