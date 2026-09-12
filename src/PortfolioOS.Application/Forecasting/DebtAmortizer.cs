using PortfolioOS.Application.Chat;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Application.Forecasting;

/// <summary>How surplus money is aimed once every minimum has been paid.</summary>
public enum PayoffStrategy
{
    /// <summary>Highest interest rate first — mathematically optimal, always the cheapest.</summary>
    Avalanche,

    /// <summary>Smallest balance first — costs more interest, closes the first account sooner.</summary>
    Snowball,

    /// <summary>
    /// Pay each minimum and nothing else. Not a strategy anyone should choose; it exists as the
    /// baseline the other two are priced against.
    /// </summary>
    MinimumOnly,
}

/// <summary>One debt as the amortiser needs it, decoupled from the persisted entity.</summary>
/// <param name="AnnualRatePercent">Annual rate as a percentage — 27 means 27%.</param>
/// <param name="MonthlyRatePercent">
/// Monthly rate as a percentage, when the debt records one. Preferred over deriving from the
/// annual figure, because it is what the lender actually charges.
/// </param>
/// <param name="Tenor">Remaining months, when known. Only used to derive an instalment.</param>
public sealed record DebtPosition(
    Guid Id,
    string Name,
    DebtType Type,
    decimal Balance,
    decimal AnnualRatePercent,
    decimal? MonthlyRatePercent,
    decimal MinimumPayment,
    int? Tenor);

/// <summary>
/// Works out when each debt is cleared and what the interest costs, by simulating the balance
/// month by month.
/// </summary>
/// <remarks>
/// This is the one projection in the app that guesses at nothing. Balance, rate and instalment are
/// recorded facts and the rest is compound interest, so the output carries no error band — only
/// the assumption that the user keeps paying what they have said they pay.
/// <para>
/// The simulation runs rather than solving for <c>n</c> in closed form, because the closed form
/// cannot express the thing that makes these strategies work: when a debt closes, its instalment
/// does not become spending money, it rolls onto the next debt in the queue. Both avalanche and
/// snowball are that same rollover, differing only in how the queue is sorted.
/// </para>
/// <para>
/// The loop is bounded by <see cref="ForecastPolicy.MaxAmortizationMonths"/> rather than by the
/// balance reaching zero, because an instalment smaller than the monthly interest never reaches
/// zero at all. That case is reported as a debt that does not pay off, which is what the recorded
/// numbers genuinely imply — a credit card at 2,25% per month against a minimum that only covers
/// the interest is a debt with no payoff date, and rounding that up to a date would be a lie.
/// </para>
/// </remarks>
public static class DebtAmortizer
{
    /// <summary>Same base currency the rest of the app reports in.</summary>
    public const string BaseCurrency = "IDR";

    /// <summary>Balances below this count as cleared, so rounding dust does not buy an extra month.</summary>
    private const decimal Epsilon = 0.01m;

    /// <param name="debts">Active debts in base currency. Zero balances are ignored.</param>
    /// <param name="today">Anchor for the schedule dates; injected so tests are reproducible.</param>
    /// <param name="extraPayment">
    /// Voluntary amount on top of the minimums, aimed at the head of the queue. Ignored under
    /// <see cref="PayoffStrategy.MinimumOnly"/>, which is by definition paying no more than asked.
    /// </param>
    public static DebtPayoffPlanDto Plan(
        IReadOnlyList<DebtPosition> debts,
        DateOnly today,
        decimal extraPayment = 0m,
        PayoffStrategy strategy = PayoffStrategy.Avalanche,
        ForecastPolicy? policy = null)
    {
        var p = policy ?? ForecastPolicy.Default;
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var active = debts.Where(d => d.Balance > Epsilon).ToList();

        if (active.Count == 0) return NoDebts(monthStart, strategy);

        var extra = Math.Max(0m, extraPayment);

        var chosen      = Simulate(active, monthStart, strategy, extra, p);
        var avalanche   = Simulate(active, monthStart, PayoffStrategy.Avalanche, extra, p);
        var snowball    = Simulate(active, monthStart, PayoffStrategy.Snowball, extra, p);
        var minimumOnly = Simulate(active, monthStart, PayoffStrategy.MinimumOnly, 0m, p);

        var items = new List<DebtPayoffItemDto>(active.Count);
        for (var rank = 0; rank < chosen.Order.Length; rank++)
        {
            var i = chosen.Order[rank];
            var d = active[i];
            var outcome = chosen.Outcomes[d.Id];

            items.Add(new DebtPayoffItemDto(
                Id: d.Id,
                Name: d.Name,
                Type: d.Type.ToString(),
                Priority: rank + 1,
                Balance: Round(d.Balance),
                AnnualRate: d.AnnualRatePercent,
                MonthlyRate: Math.Round(chosen.MonthlyRate[i] * 100m, 4),
                MinimumPayment: Round(chosen.Minimum[i]),
                MonthlyInterestAtStart: Round(d.Balance * chosen.MonthlyRate[i]),
                MonthsToPayoff: outcome.Months,
                PayoffMonth: outcome.Months is { } m ? monthStart.AddMonths(m) : null,
                TotalInterest: Round(outcome.Interest),
                PaysOff: outcome.PaidOff));
        }

        var totalBalance = active.Sum(d => d.Balance);

        var comparison = new StrategyComparisonDto(
            AvalancheMonths: avalanche.MonthsToDebtFree,
            AvalancheInterest: Round(avalanche.TotalInterest),
            SnowballMonths: snowball.MonthsToDebtFree,
            SnowballInterest: Round(snowball.TotalInterest),
            MinimumOnlyMonths: minimumOnly.MonthsToDebtFree,
            MinimumOnlyInterest: Round(minimumOnly.TotalInterest),
            InterestSavedVsMinimumOnly: Round(minimumOnly.TotalInterest - chosen.TotalInterest),
            MonthsSavedVsMinimumOnly:
                minimumOnly.MonthsToDebtFree is { } baseline && chosen.MonthsToDebtFree is { } mine
                    ? baseline - mine
                    : null,
            Better: BetterStrategy(avalanche.TotalInterest, snowball.TotalInterest));

        return new DebtPayoffPlanDto(
            HasDebts: true,
            BaseCurrency: BaseCurrency,
            Strategy: strategy.ToString(),
            StartMonth: monthStart,
            TotalBalance: Round(totalBalance),
            MonthlyMinimum: Round(chosen.Minimum.Sum()),
            ExtraPayment: Round(strategy == PayoffStrategy.MinimumOnly ? 0m : extra),
            MonthlyBudget: Round(chosen.Budget),
            TotalInterest: Round(chosen.TotalInterest),
            TotalPaid: Round(totalBalance + chosen.TotalInterest),
            MonthsToDebtFree: chosen.MonthsToDebtFree,
            DebtFreeMonth: chosen.MonthsToDebtFree is { } n ? monthStart.AddMonths(n) : null,
            AllPaidOff: chosen.AllPaidOff,
            Debts: items,
            Schedule: chosen.Schedule,
            Comparison: comparison,
            Notes: BuildNotes(active, items, chosen, avalanche, snowball, minimumOnly, strategy, extra, p));
    }

    /// <summary>Monthly rate as a fraction. The recorded monthly figure wins when there is one.</summary>
    public static decimal MonthlyRateOf(DebtPosition d, ForecastPolicy p) =>
        (d.MonthlyRatePercent ?? d.AnnualRatePercent / p.MonthsPerYear) / 100m;

    private sealed record Run(
        decimal Budget,
        decimal TotalInterest,
        int? MonthsToDebtFree,
        bool AllPaidOff,
        decimal[] MonthlyRate,
        decimal[] Minimum,
        int[] Order,
        List<DebtScheduleRowDto> Schedule,
        Dictionary<Guid, Outcome> Outcomes);

    private readonly record struct Outcome(int? Months, decimal Interest, bool PaidOff);

    private static Run Simulate(
        IReadOnlyList<DebtPosition> debts,
        DateOnly monthStart,
        PayoffStrategy strategy,
        decimal extraPayment,
        ForecastPolicy p)
    {
        // Rolling a freed instalment onto the next debt is the whole mechanism; the do-nothing
        // baseline is defined by not doing it, and by paying nothing extra either.
        var rollover = strategy != PayoffStrategy.MinimumOnly;
        var extra = rollover ? Math.Max(0m, extraPayment) : 0m;

        var n = debts.Count;
        var rate = new decimal[n];
        var minimum = new decimal[n];
        var balance = new decimal[n];
        var interestPaid = new decimal[n];
        var payoffMonth = new int?[n];

        for (var i = 0; i < n; i++)
        {
            rate[i] = MonthlyRateOf(debts[i], p);
            minimum[i] = EffectiveMinimum(debts[i], rate[i]);
            balance[i] = debts[i].Balance;
        }

        var order = PriorityOrder(debts, rate, strategy);
        var budget = minimum.Sum() + extra;

        var schedule = new List<DebtScheduleRowDto>();
        var totalInterest = 0m;

        for (var month = 1; month <= p.MaxAmortizationMonths; month++)
        {
            if (balance.All(b => b <= Epsilon)) break;

            var available = rollover
                ? budget
                : Enumerable.Range(0, n).Where(i => balance[i] > Epsilon).Sum(i => minimum[i]);

            var monthInterest = 0m;
            for (var i = 0; i < n; i++)
            {
                if (balance[i] <= Epsilon) continue;
                var interest = balance[i] * rate[i];
                balance[i] += interest;
                interestPaid[i] += interest;
                monthInterest += interest;
            }
            totalInterest += monthInterest;

            var monthPayment = 0m;

            // Minimums first, across every debt: aiming the surplus must never push another
            // account into arrears.
            for (var i = 0; i < n; i++)
            {
                if (available <= 0m) break;
                if (balance[i] <= Epsilon) continue;
                var pay = Math.Min(Math.Min(minimum[i], balance[i]), available);
                balance[i] -= pay;
                available -= pay;
                monthPayment += pay;
            }

            // Then the surplus to the head of the queue, cascading down as debts close.
            foreach (var i in order)
            {
                if (available <= 0m) break;
                if (balance[i] <= Epsilon) continue;
                var pay = Math.Min(balance[i], available);
                balance[i] -= pay;
                available -= pay;
                monthPayment += pay;
            }

            for (var i = 0; i < n; i++)
            {
                if (balance[i] > Epsilon || payoffMonth[i] is not null) continue;
                balance[i] = 0m;
                payoffMonth[i] = month;
            }

            schedule.Add(new DebtScheduleRowDto(
                MonthOffset: month,
                Month: monthStart.AddMonths(month),
                Payment: Round(monthPayment),
                Interest: Round(monthInterest),
                Principal: Round(monthPayment - monthInterest),
                RemainingBalance: Round(balance.Sum())));
        }

        var allPaidOff = balance.All(b => b <= Epsilon);

        var outcomes = new Dictionary<Guid, Outcome>(n);
        for (var i = 0; i < n; i++)
        {
            outcomes[debts[i].Id] = new Outcome(payoffMonth[i], interestPaid[i], payoffMonth[i] is not null);
        }

        return new Run(
            Budget: budget,
            TotalInterest: totalInterest,
            MonthsToDebtFree: allPaidOff
                ? payoffMonth.Where(m => m.HasValue).Select(m => m!.Value).DefaultIfEmpty(0).Max()
                : null,
            AllPaidOff: allPaidOff,
            MonthlyRate: rate,
            Minimum: minimum,
            Order: order,
            Schedule: schedule,
            Outcomes: outcomes);
    }

    /// <summary>
    /// Snowball sorts on the balance as it stands today, not as it stands each month. Re-sorting
    /// mid-run would let the queue reshuffle under a debt that is halfway paid, which is neither
    /// what the method prescribes nor what anyone following it would actually do.
    /// </summary>
    private static int[] PriorityOrder(IReadOnlyList<DebtPosition> debts, decimal[] rate, PayoffStrategy strategy)
    {
        var index = Enumerable.Range(0, debts.Count);
        return strategy switch
        {
            PayoffStrategy.Avalanche => index
                .OrderByDescending(i => rate[i]).ThenBy(i => debts[i].Balance).ToArray(),
            PayoffStrategy.Snowball => index
                .OrderBy(i => debts[i].Balance).ThenByDescending(i => rate[i]).ToArray(),
            _ => index.ToArray(),
        };
    }

    /// <summary>
    /// The instalment to simulate. A debt with no recorded minimum but a known tenor gets the
    /// annuity payment that clears it in exactly that many months — the figure the lender would
    /// have quoted. With neither, there is no instalment, and the debt only moves if surplus
    /// reaches it.
    /// </summary>
    private static decimal EffectiveMinimum(DebtPosition d, decimal monthlyRate)
    {
        if (d.MinimumPayment > 0m) return d.MinimumPayment;
        if (d.Tenor is > 0) return Annuity(d.Balance, monthlyRate, d.Tenor.Value);
        return 0m;
    }

    /// <summary>PMT = P·i / (1 − (1+i)^−n), in double: the factor is a ratio, not an amount.</summary>
    private static decimal Annuity(decimal principal, decimal monthlyRate, int months)
    {
        if (months <= 0) return principal;
        if (monthlyRate <= 0m) return principal / months;

        var i = (double)monthlyRate;
        var factor = i / (1d - Math.Pow(1d + i, -months));
        return (decimal)((double)principal * factor);
    }

    private static string BetterStrategy(decimal avalancheInterest, decimal snowballInterest) =>
        Math.Abs(avalancheInterest - snowballInterest) < 1m
            ? "Sama"
            : avalancheInterest < snowballInterest ? "Avalanche" : "Snowball";

    private static List<string> BuildNotes(
        List<DebtPosition> active, List<DebtPayoffItemDto> items, Run chosen,
        Run avalanche, Run snowball, Run minimumOnly,
        PayoffStrategy strategy, decimal extra, ForecastPolicy p)
    {
        var notes = new List<string>();

        foreach (var d in items.Where(d => !d.PaysOff))
        {
            notes.Add(
                $"\"{d.Name}\" tidak pernah lunas dengan angka yang tercatat: bunganya " +
                $"{ChatFormat.Idr(d.MonthlyInterestAtStart)} per bulan, sementara cicilannya " +
                $"{ChatFormat.Idr(d.MinimumPayment)}. Selama cicilan tidak melebihi bunga, saldonya " +
                "justru naik tiap bulan. Naikkan cicilan di atas angka bunga itu sebelum memikirkan " +
                "strategi pelunasan.");
        }

        var derived = active.Count(d => d.MinimumPayment <= 0m && d.Tenor is > 0);
        if (derived > 0)
        {
            notes.Add(
                $"{derived} utang tidak punya catatan cicilan minimum, jadi dipakai angka anuitas " +
                "yang melunasinya tepat pada akhir tenor. Isi cicilan sebenarnya kalau berbeda — " +
                "jadwalnya ikut berubah.");
        }

        var unschedulable = active.Count(d => d.MinimumPayment <= 0m && d.Tenor is not > 0);
        if (unschedulable > 0)
        {
            notes.Add(
                $"{unschedulable} utang tidak punya cicilan maupun tenor, jadi tidak ada yang bisa " +
                "dijadwalkan untuknya. Utang itu hanya berkurang kalau kebagian sisa dana.");
        }

        if (chosen.AllPaidOff && strategy != PayoffStrategy.MinimumOnly)
        {
            var saved = minimumOnly.TotalInterest - chosen.TotalInterest;
            if (saved > 1m)
            {
                var monthsSaved = minimumOnly.MonthsToDebtFree is { } b && chosen.MonthsToDebtFree is { } m
                    ? b - m
                    : (int?)null;
                var faster = monthsSaved is > 0 ? $" dan lunas {monthsSaved} bulan lebih cepat" : "";

                notes.Add(
                    $"Dibanding hanya membayar cicilan minimum tanpa menggeser apa pun, rencana ini " +
                    $"menghemat bunga {ChatFormat.Idr(saved)}{faster}. Penghematan itu datang dari dua hal " +
                    "sekaligus: setoran tambahan, dan cicilan utang yang sudah lunas yang dialihkan ke " +
                    "utang berikutnya, bukan dibelanjakan.");
            }
        }

        if (!minimumOnly.AllPaidOff && chosen.AllPaidOff)
        {
            notes.Add(
                "Kalau hanya membayar minimum, utang ini tidak pernah lunas sama sekali. Rencana ini " +
                "melunasinya — jadi selisihnya bukan soal hemat bunga, tapi soal selesai atau tidak.");
        }

        if (active.Count > 1)
        {
            var gap = Math.Abs(avalanche.TotalInterest - snowball.TotalInterest);
            if (gap < 1m)
            {
                notes.Add(
                    "Avalanche dan Snowball menghasilkan biaya bunga yang sama persis di sini, jadi " +
                    "pilih yang lebih enak dijalani.");
            }
            else
            {
                notes.Add(
                    $"Avalanche (bunga tertinggi dulu) menghabiskan bunga " +
                    $"{ChatFormat.Idr(avalanche.TotalInterest)}, Snowball (saldo terkecil dulu) " +
                    $"{ChatFormat.Idr(snowball.TotalInterest)} — selisih {ChatFormat.Idr(gap)}. Avalanche " +
                    "selalu lebih murah secara matematika; Snowball menutup akun pertama lebih cepat, dan " +
                    "buat sebagian orang itu yang bikin rencananya bertahan.");
            }
        }

        if (extra > 0m && strategy == PayoffStrategy.MinimumOnly)
        {
            notes.Add(
                $"Setoran tambahan {ChatFormat.Idr(extra)} diabaikan karena strategi yang dipilih adalah " +
                "membayar minimum saja.");
        }

        if (!chosen.AllPaidOff && items.All(d => d.PaysOff))
        {
            notes.Add(
                $"Simulasi dihentikan di {p.MaxAmortizationMonths} bulan tanpa semua utang lunas. " +
                "Periksa lagi saldo, bunga, dan cicilan yang tercatat.");
        }

        var longest = items.FirstOrDefault(d => d.MonthsToPayoff is > 120);
        if (longest is not null)
        {
            notes.Add(
                $"\"{longest.Name}\" butuh lebih dari 10 tahun. Untuk utang sepanjang itu, jadwal ini " +
                "mengasumsikan bunganya tidak berubah sama sekali — pada KPR floating, asumsi itu yang " +
                "paling dulu meleset.");
        }

        return notes;
    }

    private static DebtPayoffPlanDto NoDebts(DateOnly monthStart, PayoffStrategy strategy) => new(
        HasDebts: false,
        BaseCurrency: BaseCurrency,
        Strategy: strategy.ToString(),
        StartMonth: monthStart,
        TotalBalance: 0m, MonthlyMinimum: 0m, ExtraPayment: 0m, MonthlyBudget: 0m,
        TotalInterest: 0m, TotalPaid: 0m,
        MonthsToDebtFree: 0, DebtFreeMonth: monthStart, AllPaidOff: true,
        Debts: [], Schedule: [], Comparison: null,
        Notes: ["Tidak ada utang aktif dalam rupiah yang tercatat, jadi tidak ada yang dijadwalkan."]);

    private static decimal Round(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero);
}
