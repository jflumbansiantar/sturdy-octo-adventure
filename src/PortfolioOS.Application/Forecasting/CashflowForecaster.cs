using PortfolioOS.Application.Chat;
using PortfolioOS.Application.Savings;

namespace PortfolioOS.Application.Forecasting;

/// <summary>
/// Projects the monthly cash flow forward, as a band rather than a line.
/// </summary>
/// <remarks>
/// Each component of <see cref="MonthlyCashflow"/> is projected the way its own behaviour warrants,
/// because they do not behave alike:
/// <list type="bullet">
/// <item><b>Pemasukan</b> — held at the median of the baseline. A salary is a step function, not a
/// trend: it sits flat and then jumps. Fitting a slope to it forecasts a raise that was never
/// promised, so the level is carried forward and a real raise arrives as a new level.</item>
/// <item><b>Kebutuhan</b> — median, drifting with inflation. Inelastic spending genuinely does
/// creep up with prices.</item>
/// <item><b>Keinginan</b> — median, flat. Prices rise here too, but discretionary spending is
/// elastic: people substitute rather than pay more. Inflating both halves against a flat income
/// manufactures a deficit out of the assumption alone, so only the half that cannot be substituted
/// gets the drift.</item>
/// <item><b>Cicilan</b> — not forecast at all. It comes from <see cref="DebtAmortizer"/>, so the
/// month a debt is cleared is the month the outgoing actually stops.</item>
/// </list>
/// <para>
/// The median is used throughout rather than the mean, for the same reason the band is built from a
/// median absolute deviation: with at most twenty-four observations, one holiday or one bonus would
/// otherwise set the level for the entire horizon.
/// </para>
/// <para>
/// <b>What this deliberately does not do</b> is fit a trend, a seasonal term, or an ARIMA. Monthly
/// household data tops out at 24 points here, and at that length those models fit the noise and
/// report the fit as confidence. A level model with an honest band is not a simplification of a
/// better method — at this sample size it <i>is</i> the better method, and it is the same judgement
/// <see cref="SavingsPolicy.MinRegressionMonths"/> already makes about regressing three points.
/// </para>
/// </remarks>
public static class CashflowForecaster
{
    /// <summary>Same base currency the rest of the app reports in.</summary>
    public const string BaseCurrency = "IDR";

    /// <param name="history">Observed months, in any order. Months without income are ignored.</param>
    /// <param name="today">Anchor for the projected dates; injected so tests are reproducible.</param>
    /// <param name="scheduledDebtService">
    /// Instalments per future month from <see cref="DebtAmortizer"/>, index 0 being next month.
    /// When supplied, months past the end of the schedule carry no instalment at all — the debt is
    /// paid off by then, and that drop is the single most useful thing in the projection. When
    /// omitted, the recorded median is carried forward flat instead.
    /// </param>
    /// <param name="openingCash">
    /// Spendable cash the cumulative column starts from. Zero turns that column into a cumulative
    /// change rather than a balance, and suppresses the "cash runs out" date, which would otherwise
    /// be reported against a balance of nothing.
    /// </param>
    public static CashflowForecastDto Forecast(
        IReadOnlyList<MonthlyCashflow> history,
        DateOnly today,
        IReadOnlyList<decimal>? scheduledDebtService = null,
        decimal openingCash = 0m,
        int? horizonMonths = null,
        ForecastPolicy? policy = null)
    {
        var p = policy ?? ForecastPolicy.Default;
        var horizon = Math.Clamp(horizonMonths ?? p.HorizonMonths, 1, p.MaxHorizonMonths);
        var observed = history.OrderBy(m => m.Month).ToList();

        // Same rule the savings advisor applies: a month with income and no spending was not
        // logged, and reading it as thrift would lift the whole projection.
        var months = observed.Where(m => m.Income > 0 && m.Consumption > 0).ToList();
        var unlogged = observed.Count(m => m.Income > 0 && m.Consumption <= 0);

        if (months.Count == 0) return NoData(observed, unlogged, horizon, openingCash);

        var baseline = months.TakeLast(p.BaselineMonths).ToList();
        var income = Median(baseline.Select(m => m.Income));
        var needs = Median(baseline.Select(m => m.Needs));
        var wants = Median(baseline.Select(m => m.Wants));
        var recordedDebt = Median(baseline.Select(m => m.DebtService));

        // Dispersion is measured across every usable month, not just the baseline: the band is
        // about how much this household varies, and a longer window measures that better.
        var nets = months.Select(m => m.Saving).ToList();
        var mad = MedianAbsoluteDeviation(nets);
        var canMeasure = months.Count >= p.MinMonthsForBand && mad > 0m;
        var sigma = canMeasure ? p.MadToSigma * mad : p.FallbackDispersionShare * income;
        var band = p.BandZ * sigma;

        var hasSchedule = scheduledDebtService is { Count: > 0 };
        var monthlyInflation = p.MonthlyInflationRate;
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var rows = new List<CashflowForecastMonthDto>(horizon);
        var cumulative = openingCash;

        for (var h = 1; h <= horizon; h++)
        {
            var needsAt = needs * (decimal)Math.Pow(1d + (double)monthlyInflation, h);
            var wantsAt = wants;

            // Past the end of the schedule the debt is cleared, so the instalment is genuinely
            // zero — falling back to the recorded median here would keep charging a paid-off loan.
            var debtAt = hasSchedule
                ? h - 1 < scheduledDebtService!.Count ? scheduledDebtService[h - 1] : 0m
                : recordedDebt;

            var net = income - needsAt - wantsAt - debtAt;
            cumulative += net;

            // Independent monthly errors add in quadrature, so the cumulative band opens as √h
            // while the monthly band stays the same width.
            var cumulativeBand = p.BandZ * sigma * (decimal)Math.Sqrt(h);

            rows.Add(new CashflowForecastMonthDto(
                MonthOffset: h,
                Month: monthStart.AddMonths(h),
                Income: Round(income),
                Needs: Round(needsAt),
                Wants: Round(wantsAt),
                DebtService: Round(debtAt),
                NetP50: Round(net),
                NetP10: Round(net - band),
                NetP90: Round(net + band),
                CumulativeP50: Round(cumulative),
                CumulativeP10: Round(cumulative - cumulativeBand),
                CumulativeP90: Round(cumulative + cumulativeBand)));
        }

        var firstDeficit = rows.FirstOrDefault(r => r.NetP50 < 0m);

        // Only meaningful against a real balance. With no recognised cash account the cumulative
        // column is a change from zero, and "cash runs out" would fire on the first negative month.
        var runsOut = openingCash > 0m
            ? rows.FirstOrDefault(r => r.CumulativeP50 < 0m)
            : null;

        var burn = needs + wants + (hasSchedule ? scheduledDebtService![0] : recordedDebt);
        var runway = openingCash > 0m && burn > 0m ? (int)Math.Floor(openingCash / burn) : (int?)null;

        var expectedNet = rows.Count > 0 ? rows[0].NetP50 : 0m;

        return new CashflowForecastDto(
            HasEnoughData: true,
            MonthsAnalyzed: months.Count,
            BaselineMonths: baseline.Count,
            HorizonMonths: horizon,
            BaseCurrency: BaseCurrency,
            OpeningCash: Round(openingCash),
            MonthlyIncome: Round(income),
            MonthlyNeeds: Round(needs),
            MonthlyWants: Round(wants),
            MonthlyDebtService: Round(hasSchedule ? scheduledDebtService![0] : recordedDebt),
            ExpectedMonthlyNet: expectedNet,
            Dispersion: Round(sigma),
            DispersionIsAssumed: !canMeasure,
            AnnualInflationRate: Math.Round(p.AnnualInflationRate * 100m, 2),
            DebtServiceIsScheduled: hasSchedule,
            FirstDeficitMonth: firstDeficit?.Month,
            CashRunsOutMonth: runsOut?.Month,
            RunwayMonths: runway,
            Forecast: rows,
            History: [.. observed.Select(ToDto)],
            Notes: BuildNotes(
                p, months.Count, unlogged, income, needs, wants, recordedDebt, sigma, canMeasure,
                openingCash, runway, firstDeficit, runsOut, hasSchedule, rows, horizon));
    }

    private static List<string> BuildNotes(
        ForecastPolicy p, int monthCount, int unlogged, decimal income, decimal needs, decimal wants,
        decimal recordedDebt, decimal sigma, bool canMeasure, decimal openingCash, int? runway,
        CashflowForecastMonthDto? firstDeficit, CashflowForecastMonthDto? runsOut,
        bool hasSchedule, List<CashflowForecastMonthDto> rows, int horizon)
    {
        var notes = new List<string>();

        if (monthCount < p.BaselineMonths)
        {
            notes.Add(
                $"Baru {monthCount} bulan data yang terpakai dari {p.BaselineMonths} yang ideal, jadi " +
                "level awalnya masih mudah bergeser. Setiap bulan baru yang dicatat akan " +
                "mempersempit pita ini.");
        }

        if (unlogged > 0)
        {
            notes.Add(
                $"{unlogged} bulan punya catatan pemasukan tanpa satu pun pengeluaran, jadi dilewati — " +
                "kalau ikut dihitung, proyeksinya terbaca terlalu optimis.");
        }

        if (!canMeasure)
        {
            notes.Add(
                $"Sebaran bulanan belum bisa diukur dari riwayat (butuh minimal {p.MinMonthsForBand} " +
                $"bulan yang bervariasi), jadi dipakai asumsi {ChatFormat.Rate(p.FallbackDispersionShare * 100m)} " +
                "dari pemasukan. Pita P10-P90 di sini sengaja dibuat lebar: ketidakpastian yang " +
                "belum terukur tidak pantas terlihat sempit.");
        }
        else
        {
            notes.Add(
                $"Arus kas bulanan Anda bergerak sekitar {ChatFormat.Idr(sigma)} naik-turun dari " +
                "bulan ke bulan. Itulah lebar pita P10-P90 — bukan hasil model, tapi ukuran " +
                "seberapa tidak seragam bulan-bulan Anda sendiri.");
        }

        if (firstDeficit is not null)
        {
            notes.Add(
                $"Mulai {ChatFormat.Date(firstDeficit.Month)} pengeluaran diproyeksikan melebihi " +
                $"pemasukan ({ChatFormat.SignedIdr(firstDeficit.NetP50)} per bulan). Ini proyeksi, " +
                "bukan kepastian — tapi bulan itu yang perlu disiapkan dari sekarang.");
        }
        else if (rows.Count > 0)
        {
            notes.Add(
                $"Tidak ada bulan defisit dalam {horizon} bulan ke depan pada jalur tengah. " +
                $"Pada batas bawah pita (P10), surplus bulanan bisa turun sampai " +
                $"{ChatFormat.SignedIdr(rows[^1].NetP10)}.");
        }

        if (runsOut is not null)
        {
            notes.Add(
                $"Kas diproyeksikan habis sekitar {ChatFormat.Date(runsOut.Month)}. Angka ini yang " +
                "paling layak ditindaklanjuti di seluruh halaman ini.");
        }

        if (openingCash <= 0m)
        {
            notes.Add(
                "Tidak ada akun kas atau bank yang dikenali di buku besar, jadi kolom kumulatif " +
                "dibaca sebagai perubahan kas, bukan saldo. Beri nama akun yang mengandung " +
                "\"Kas\" atau \"Bank\" supaya saldo awal dan sisa napas ikut terhitung.");
        }
        else if (runway is not null)
        {
            notes.Add(
                $"Kalau pemasukan berhenti hari ini, kas {ChatFormat.Idr(openingCash)} menutupi " +
                $"sekitar {runway} bulan pengeluaran. Patokan umum dana darurat adalah " +
                $"{p.EmergencyFundMonths} bulan.");
        }

        if (hasSchedule)
        {
            var lastWithDebt = rows.LastOrDefault(r => r.DebtService > 0m);
            if (lastWithDebt is not null && lastWithDebt.MonthOffset < horizon)
            {
                notes.Add(
                    $"Cicilan diambil dari jadwal amortisasi, bukan dirata-rata — itu sebabnya " +
                    $"beban cicilan hilang setelah {ChatFormat.Date(lastWithDebt.Month)} dan arus kas " +
                    "melompat naik di bulan berikutnya.");
            }
        }
        else if (recordedDebt > 0m)
        {
            notes.Add(
                $"Cicilan {ChatFormat.Idr(recordedDebt)} per bulan dibawa datar karena jadwal " +
                "pelunasan tidak disertakan. Dengan jadwal itu, proyeksinya akan menunjukkan kapan " +
                "beban ini berhenti.");
        }

        notes.Add(
            $"Pemasukan diproyeksikan datar di {ChatFormat.Idr(income)} — kenaikan gaji tidak " +
            "ditebak, dan akan masuk sebagai level baru begitu benar-benar tercatat. Kebutuhan " +
            $"merayap {ChatFormat.Rate(p.AnnualInflationRate * 100m)} per tahun mengikuti inflasi, " +
            "sementara keinginan dibiarkan datar karena pengeluaran jenis itu bisa digeser.");

        return notes;
    }

    private static CashflowForecastDto NoData(
        List<MonthlyCashflow> observed, int unlogged, int horizon, decimal openingCash)
    {
        var notes = new List<string>
        {
            "Belum ada bulan dengan catatan pemasukan sekaligus pengeluaran, jadi belum ada yang " +
            "bisa diproyeksikan. Catat transaksi Income dan Expense minimal satu bulan penuh.",
        };

        if (unlogged > 0)
        {
            notes.Add(
                $"{unlogged} bulan sudah punya pemasukan tetapi pengeluarannya belum dicatat sama sekali.");
        }

        return new CashflowForecastDto(
            HasEnoughData: false,
            MonthsAnalyzed: 0,
            BaselineMonths: 0,
            HorizonMonths: horizon,
            BaseCurrency: BaseCurrency,
            OpeningCash: Round(openingCash),
            MonthlyIncome: 0m, MonthlyNeeds: 0m, MonthlyWants: 0m, MonthlyDebtService: 0m,
            ExpectedMonthlyNet: 0m,
            Dispersion: 0m, DispersionIsAssumed: true,
            AnnualInflationRate: 0m,
            DebtServiceIsScheduled: false,
            FirstDeficitMonth: null, CashRunsOutMonth: null, RunwayMonths: null,
            Forecast: [],
            History: [.. observed.Select(ToDto)],
            Notes: notes);
    }

    private static MonthlyCashflowDto ToDto(MonthlyCashflow m) => new(
        m.Month, Round(m.Income), Round(m.Needs), Round(m.DebtService), Round(m.Wants),
        Round(m.Saving), m.Income <= 0 ? 0m : Math.Round(m.Saving / m.Income * 100m, 2));

    private static decimal Median(IEnumerable<decimal> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        if (sorted.Count == 0) return 0m;

        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2m;
    }

    /// <summary>
    /// Median of the absolute deviations from the median — a spread measure that one extreme month
    /// cannot inflate, unlike the standard deviation it stands in for.
    /// </summary>
    private static decimal MedianAbsoluteDeviation(IReadOnlyList<decimal> values)
    {
        if (values.Count == 0) return 0m;

        var median = Median(values);
        return Median(values.Select(v => Math.Abs(v - median)));
    }

    private static decimal Round(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero);
}
