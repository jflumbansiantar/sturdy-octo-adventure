using PortfolioOS.Application.Chat;

namespace PortfolioOS.Application.Savings;

/// <summary>One month of household cash flow, already split the way the 50/30/20 rule needs it.</summary>
/// <param name="Needs">Essential spending other than debt service.</param>
/// <param name="Wants">Discretionary spending — the only part the advisor ever assumes can shrink.</param>
/// <param name="DebtService">Instalments paid. Essential too, but tracked apart so it can be reported.</param>
public sealed record MonthlyCashflow(
    DateOnly Month,
    decimal Income,
    decimal Needs,
    decimal Wants,
    decimal DebtService)
{
    public decimal Essentials => Needs + DebtService;
    public decimal Consumption => Needs + Wants + DebtService;
    public decimal Saving => Income - Consumption;
}

/// <summary>
/// Works out how much someone can realistically save each month by combining three methods that
/// each answer a different question.
/// </summary>
/// <remarks>
/// <list type="number">
/// <item>
/// <b>Alokasi proporsional (50/30/20)</b> answers <i>how much should they save</i>. It is
/// normative: 20% of income, whoever they are. Used alone it is deaf to a household whose
/// essentials already eat 70% of income, so its answer is capped by what is actually left over.
/// </item>
/// <item>
/// <b>Proporsi konsumsi Keynesian (MPS)</b> answers <i>how much do they in fact save</i>. Fitting
/// <c>C = a + b·Y</c> over the recorded months turns their own behaviour into a number: savings
/// follow as <c>S = (1 − b)·Y − a</c>. A high MPS is evidence the 20% target understates them; a
/// low one is evidence it overstates them. It only ever gets a partial vote (<see
/// cref="SavingsPolicy.MaxMpsWeight"/>), weighted by how well the line fits and how many months
/// there are, because it describes a habit rather than a goal.
/// </item>
/// <item>
/// <b>Save More Tomorrow</b> answers <i>how fast can they get there</i>. It contributes no target
/// at all: it takes the gap between what is saved today and the blended target and turns it into a
/// ramp, funded first from any pay rise and then from a one-point-a-month escalation. That is the
/// whole insight of Thaler and Benartzi — a plan that asks for a cut in take-home pay today loses
/// to loss aversion, one funded out of money not yet spent does not.
/// </item>
/// </list>
/// <para>
/// So the target is <c>(1 − w)·S_50/30/20 + w·S_MPS</c>, bounded by capacity, and what the user is
/// asked to do this month is <c>S_now + raise capture + escalation</c>, bounded by that target.
/// </para>
/// </remarks>
public static class SavingsAdvisor
{
    /// <summary>Same base currency the rest of the app reports in.</summary>
    public const string BaseCurrency = "IDR";

    /// <param name="history">Observed months, in any order. Months without income are ignored.</param>
    /// <param name="today">Anchor for the dates on the ramp; injected so tests are reproducible.</param>
    /// <param name="committedDebtPayment">
    /// Minimum monthly payment across active debts. Used as a floor for debt service, so a month
    /// where an instalment was simply not recorded cannot inflate the amount on offer.
    /// </param>
    public static SavingsSuggestionDto Recommend(
        IReadOnlyList<MonthlyCashflow> history,
        DateOnly today,
        decimal committedDebtPayment = 0m,
        SavingsPolicy? policy = null)
    {
        var p = policy ?? SavingsPolicy.Default;
        var observed = history.OrderBy(m => m.Month).ToList();

        // A month with income but nothing spent is a month that was not logged, not a month of
        // perfect thrift. Counting it would report a 100% savings rate and then target from there.
        var months = observed.Where(m => m.Income > 0 && m.Consumption > 0).ToList();
        var unlogged = observed.Count(m => m.Income > 0 && m.Consumption <= 0);

        if (months.Count == 0)
            return NoData(observed, unlogged);

        var baseline = months.TakeLast(p.BaselineMonths).ToList();
        var income = Mean(baseline.Select(m => m.Income));
        var needs  = Mean(baseline.Select(m => m.Needs));
        var wants  = Mean(baseline.Select(m => m.Wants));
        var recordedDebt = Mean(baseline.Select(m => m.DebtService));
        var debt = Math.Max(recordedDebt, committedDebtPayment);

        var essentials    = needs + debt;
        var currentSaving = Math.Max(0m, income - essentials - wants);

        // Everything below is bounded by this: essentials are untouchable, and only half of
        // discretionary spending is ever assumed away.
        var capacity = Math.Max(0m, income - essentials - wants * (1m - p.MaxWantsCut));

        // ── Leg 1: 50/30/20 ────────────────────────────────────────────────────────────────
        var idealSaving    = income * p.TargetSavingShare;
        var feasibleSaving = Math.Min(idealSaving, capacity);

        // ── Leg 2: Keynesian consumption function ──────────────────────────────────────────
        var fit = FitConsumption(months, p);
        var mps = 1m - fit.Mpc;
        var mpsSaving = Math.Clamp(mps * income - fit.Autonomous, 0m, capacity);

        var confidence = fit.IsRegression
            ? fit.RSquared * Math.Min(1m, months.Count / (decimal)Math.Max(1, p.FullConfidenceMonths))
            : p.FallbackConfidence;
        var mpsWeight = p.MaxMpsWeight * confidence;

        // ── Blend: the normative anchor, pulled towards observed behaviour ─────────────────
        var blendedTarget = Math.Clamp(
            (1m - mpsWeight) * feasibleSaving + mpsWeight * mpsSaving, 0m, capacity);

        // Never tell someone who already saves more than the target to save less.
        var alreadyAhead = currentSaving >= blendedTarget;
        var target = alreadyAhead ? currentSaving : blendedTarget;

        // ── Leg 3: Save More Tomorrow ramp ─────────────────────────────────────────────────
        var raise = RecentRaise(months, income, p);
        var raiseCapture = raise * p.RaiseCaptureRate;
        var escalation = income * p.EscalationRate;

        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var ramp = new List<SavingsRampStepDto>(p.HorizonMonths);
        for (var k = 1; k <= p.HorizonMonths; k++)
        {
            var amount = Math.Min(target, currentSaving + raiseCapture + escalation * k);
            var rounded = RoundDown(Math.Clamp(amount, 0m, capacity), p.RoundingStep);
            ramp.Add(new SavingsRampStepDto(k, monthStart.AddMonths(k), rounded, Share(rounded, income)));
        }

        var gap = target - currentSaving - raiseCapture;
        var monthsToTarget = gap <= 0 ? 1 : escalation <= 0 ? 0 : (int)Math.Ceiling(gap / escalation);
        var reachesTarget = monthsToTarget >= 1 && monthsToTarget <= p.HorizonMonths;

        // The headline is the first rung, not the target: it is what to do once this page closes.
        var recommended = ramp[0].Amount;
        var roundedTarget = RoundDown(target, p.RoundingStep);

        var notes = BuildNotes(
            p, income, needs, wants, debt, recordedDebt, committedDebtPayment, currentSaving,
            capacity, idealSaving, feasibleSaving, blendedTarget, alreadyAhead, raise,
            fit.IsRegression, months.Count, unlogged);

        return new SavingsSuggestionDto(
            HasEnoughData: true,
            MonthsAnalyzed: months.Count,
            BaselineMonths: baseline.Count,
            BaseCurrency: BaseCurrency,
            MonthlyIncome: Round(income),
            MonthlyNeeds: Round(needs),
            MonthlyDebtService: Round(debt),
            MonthlyWants: Round(wants),
            CurrentSaving: Round(currentSaving),
            CurrentSavingRate: Share(currentSaving, income),
            RecommendedSaving: recommended,
            RecommendedSavingRate: Share(recommended, income),
            TargetSaving: roundedTarget,
            TargetSavingRate: Share(roundedTarget, income),
            MaxCapacity: Round(capacity),
            Allocation: new AllocationMethodDto(
                EssentialsShare: Share(essentials, income),
                WantsShare: Share(wants, income),
                SavingShare: Share(currentSaving, income),
                EssentialsCeiling: p.NeedsShare * 100m,
                WantsCeiling: p.WantsShare * 100m,
                TargetShare: p.TargetSavingShare * 100m,
                IdealSaving: Round(idealSaving),
                FeasibleSaving: Round(feasibleSaving),
                EssentialsOverCeiling: income > 0 && essentials > income * p.NeedsShare,
                WantsOverCeiling: income > 0 && wants > income * p.WantsShare),
            Mps: new MpsMethodDto(
                Mpc: Math.Round(fit.Mpc, 4),
                Mps: Math.Round(mps, 4),
                AutonomousConsumption: Round(fit.Autonomous),
                RSquared: Math.Round(fit.RSquared * 100m, 2),
                Confidence: Math.Round(confidence * 100m, 2),
                Weight: Math.Round(mpsWeight * 100m, 2),
                Saving: Round(mpsSaving),
                IsRegression: fit.IsRegression),
            Smart: new SmartMethodDto(
                Raise: Round(raise),
                RaiseCaptureRate: p.RaiseCaptureRate * 100m,
                EscalationRate: p.EscalationRate * 100m,
                RaiseCapture: Round(raiseCapture),
                MonthlyStep: Round(raiseCapture + escalation),
                MonthsToTarget: monthsToTarget,
                ReachesTargetWithinHorizon: reachesTarget),
            Ramp: ramp,
            History: [.. observed.Select(ToDto)],
            Notes: notes);
    }

    /// <summary>
    /// Income growth of the baseline block over the block before it. Only a rise counts: SMarT
    /// escalates on raises and stays put otherwise, it never claws savings back on a bad month.
    /// </summary>
    private static decimal RecentRaise(List<MonthlyCashflow> months, decimal income, SavingsPolicy p)
    {
        if (months.Count < p.BaselineMonths * 2) return 0m;

        var previous = months
            .Skip(months.Count - p.BaselineMonths * 2)
            .Take(p.BaselineMonths)
            .Select(m => m.Income);

        return Math.Max(0m, income - Mean(previous));
    }

    private readonly record struct ConsumptionFit(
        decimal Mpc, decimal Autonomous, decimal RSquared, bool IsRegression);

    /// <summary>
    /// Fits <c>C = a + b·Y</c> by least squares, falling back to the average propensity to consume
    /// (<c>a = 0</c>, <c>b = ΣC/ΣY</c>) when the sample is too short, income never moved, or the
    /// slope comes back outside the range a consumption function can plausibly take.
    /// </summary>
    /// <remarks>Fitted in double: squared rupiah amounts get large, and the result is a ratio.</remarks>
    private static ConsumptionFit FitConsumption(List<MonthlyCashflow> months, SavingsPolicy p)
    {
        var totalIncome = months.Sum(m => m.Income);
        var averagePropensity = totalIncome > 0
            ? Math.Clamp(months.Sum(m => m.Consumption) / totalIncome, p.MinMpc, p.MaxMpc)
            : p.MaxMpc;

        if (months.Count < p.MinRegressionMonths)
            return new ConsumptionFit(averagePropensity, 0m, 0m, IsRegression: false);

        var xs = months.Select(m => (double)m.Income).ToArray();
        var ys = months.Select(m => (double)m.Consumption).ToArray();
        var meanX = xs.Average();
        var meanY = ys.Average();

        var sxx = xs.Sum(x => (x - meanX) * (x - meanX));
        var syy = ys.Sum(y => (y - meanY) * (y - meanY));
        var sxy = xs.Zip(ys, (x, y) => (x - meanX) * (y - meanY)).Sum();

        // A flat income series identifies no slope at all — every line through it fits equally.
        if (sxx <= 0d || double.IsNaN(sxy))
            return new ConsumptionFit(averagePropensity, 0m, 0m, IsRegression: false);

        var slope = sxy / sxx;
        if (slope <= 0d || slope > 1d)
        {
            // Consumption falling as income rises, or rising faster than income does, is noise in
            // a handful of months rather than a propensity worth extrapolating from.
            return new ConsumptionFit(averagePropensity, 0m, 0m, IsRegression: false);
        }

        var intercept = meanY - slope * meanX;
        var rSquared = syy <= 0d ? 0d : Math.Clamp(sxy * sxy / (sxx * syy), 0d, 1d);

        return new ConsumptionFit(
            Math.Clamp((decimal)slope, p.MinMpc, p.MaxMpc),
            (decimal)intercept,
            (decimal)rSquared,
            IsRegression: true);
    }

    private static List<string> BuildNotes(
        SavingsPolicy p, decimal income, decimal needs, decimal wants, decimal debt,
        decimal recordedDebt, decimal committedDebt, decimal currentSaving, decimal capacity,
        decimal idealSaving, decimal feasibleSaving, decimal blendedTarget, bool alreadyAhead,
        decimal raise, bool isRegression, int monthCount, int unlogged)
    {
        var notes = new List<string>();

        if (monthCount < p.BaselineMonths)
        {
            notes.Add(
                $"Baru {monthCount} bulan data yang terpakai, jadi angkanya masih kasar. " +
                $"Setelah {p.FullConfidenceMonths} bulan, pola konsumsi Anda ikut diperhitungkan penuh.");
        }

        if (unlogged > 0)
        {
            notes.Add(
                $"{unlogged} bulan punya catatan pemasukan tanpa satu pun pengeluaran, jadi dilewati — " +
                "kalau ikut dihitung, bulan itu terbaca seolah Anda menabung 100%.");
        }

        var essentials = needs + debt;
        if (income > 0 && essentials > income * p.NeedsShare)
        {
            notes.Add(
                $"Kebutuhan pokok dan cicilan menyerap {ChatFormat.Rate(Share(essentials, income))} " +
                $"dari pemasukan, di atas pagu {ChatFormat.Rate(p.NeedsShare * 100m)} aturan 50/30/20. " +
                "Target tabungan diturunkan supaya tetap bisa dijalankan, bukan dipaksakan.");
        }

        if (income > 0 && wants > income * p.WantsShare)
        {
            notes.Add(
                $"Pengeluaran gaya hidup {ChatFormat.Rate(Share(wants, income))} dari pemasukan " +
                $"(pagu {ChatFormat.Rate(p.WantsShare * 100m)}). Memangkasnya sampai pagu itu " +
                $"membebaskan {ChatFormat.Idr(wants - income * p.WantsShare)} per bulan.");
        }

        if (income > 0 && debt > income * 0.30m)
        {
            notes.Add(
                $"Cicilan utang {ChatFormat.Rate(Share(debt, income))} dari pemasukan, di atas batas " +
                "aman 30%. Melunasi utang berbunga tinggi biasanya menaikkan kapasitas menabung " +
                "lebih cepat daripada menambah setoran.");
        }

        if (committedDebt > recordedDebt)
        {
            notes.Add(
                $"Kewajiban minimum utang aktif {ChatFormat.Idr(committedDebt)} per bulan, lebih besar " +
                $"dari cicilan yang tercatat ({ChatFormat.Idr(recordedDebt)}). Yang dipakai angka " +
                "kewajibannya, supaya saran ini tidak menghitung uang yang sudah ada pemiliknya.");
        }

        if (feasibleSaving < idealSaving)
        {
            notes.Add(
                $"20% ideal aturan 50/30/20 setara {ChatFormat.Idr(idealSaving)}, sementara kapasitas " +
                $"maksimal Anda {ChatFormat.Idr(capacity)} per bulan. Yang dipakai kapasitasnya.");
        }

        if (alreadyAhead)
        {
            notes.Add(
                $"Anda sudah menyisihkan {ChatFormat.Rate(Share(currentSaving, income))} dari pemasukan, " +
                $"di atas target gabungan {ChatFormat.Idr(blendedTarget)}. Saran ini jadi soal " +
                "mempertahankannya, bukan menaikkannya.");
        }

        if (raise > 0)
        {
            notes.Add(
                $"Pemasukan Anda naik {ChatFormat.Idr(raise)} dibanding periode sebelumnya. " +
                $"{ChatFormat.Rate(p.RaiseCaptureRate * 100m)} dari kenaikan itu langsung dialihkan ke " +
                "tabungan — inti Save More Tomorrow: tambahan setoran diambil dari uang yang belum " +
                "sempat Anda biasakan untuk dibelanjakan.");
        }

        if (!isRegression)
        {
            notes.Add(
                "Fungsi konsumsi belum bisa diregresi (data terlalu pendek atau pemasukan tidak " +
                "berubah), jadi MPS dihitung dari rata-rata dan bobotnya sengaja dikecilkan.");
        }

        return notes;
    }

    private static SavingsSuggestionDto NoData(List<MonthlyCashflow> observed, int unlogged)
    {
        var notes = new List<string>
        {
            "Belum ada bulan dengan catatan pemasukan sekaligus pengeluaran, jadi belum ada yang " +
            "bisa dihitung. Catat transaksi Income dan Expense minimal satu bulan penuh.",
        };

        if (unlogged > 0)
        {
            notes.Add(
                $"{unlogged} bulan sudah punya pemasukan tetapi pengeluarannya belum dicatat sama sekali.");
        }

        return new SavingsSuggestionDto(
            HasEnoughData: false,
            MonthsAnalyzed: 0,
            BaselineMonths: 0,
            BaseCurrency: BaseCurrency,
            MonthlyIncome: 0m, MonthlyNeeds: 0m, MonthlyDebtService: 0m, MonthlyWants: 0m,
            CurrentSaving: 0m, CurrentSavingRate: 0m,
            RecommendedSaving: 0m, RecommendedSavingRate: 0m,
            TargetSaving: 0m, TargetSavingRate: 0m, MaxCapacity: 0m,
            Allocation: new AllocationMethodDto(0m, 0m, 0m, 50m, 30m, 20m, 0m, 0m, false, false),
            Mps: new MpsMethodDto(0m, 0m, 0m, 0m, 0m, 0m, 0m, false),
            Smart: new SmartMethodDto(0m, 0m, 0m, 0m, 0m, 0, false),
            Ramp: [],
            History: [.. observed.Select(ToDto)],
            Notes: notes);
    }

    private static MonthlyCashflowDto ToDto(MonthlyCashflow m) => new(
        m.Month, Round(m.Income), Round(m.Needs), Round(m.DebtService), Round(m.Wants),
        Round(m.Saving), Share(m.Saving, m.Income));

    private static decimal Mean(IEnumerable<decimal> values)
    {
        var list = values as IList<decimal> ?? values.ToList();
        return list.Count == 0 ? 0m : list.Sum() / list.Count;
    }

    /// <summary>Percentage of income, the way the rest of the app reports rates (20 means 20%).</summary>
    private static decimal Share(decimal part, decimal income) =>
        income <= 0 ? 0m : Math.Round(part / income * 100m, 2);

    private static decimal Round(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero);

    private static decimal RoundDown(decimal value, decimal step) =>
        step <= 0 ? Round(value) : Math.Floor(value / step) * step;
}
