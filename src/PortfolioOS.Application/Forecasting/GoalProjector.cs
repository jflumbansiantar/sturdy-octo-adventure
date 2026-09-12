using PortfolioOS.Application.Chat;

namespace PortfolioOS.Application.Forecasting;

/// <summary>When a savings target is reached, given what is actually being set aside each month.</summary>
/// <param name="IsReachable">
/// False when nothing is being contributed and the target is still short — there is no date to
/// report, and inventing one by assuming a contribution nobody has made would be the whole error
/// this app tries not to commit.
/// </param>
/// <param name="MonthlyContribution">The steady-state contribution once the ramp has topped out.</param>
/// <param name="ProgressShare">How much of the target is already funded, as a percentage.</param>
public record GoalProjectionDto(
    bool IsReachable,
    string BaseCurrency,
    string GoalName,
    decimal TargetAmount,
    decimal CurrentAmount,
    decimal Shortfall,
    decimal MonthlyContribution,
    int? MonthsToTarget,
    DateOnly? TargetMonth,
    decimal ProgressShare,
    IReadOnlyList<GoalMilestoneDto> Trajectory,
    IReadOnlyList<string> Notes);

/// <summary>One month of the accumulation path.</summary>
public record GoalMilestoneDto(
    int MonthOffset,
    DateOnly Month,
    decimal Contribution,
    decimal Balance,
    decimal ProgressShare);

/// <summary>
/// Turns a monthly savings figure into a date.
/// </summary>
/// <remarks>
/// This is the last step of the chain rather than a model of its own: <see cref="Savings.SavingsAdvisor"/>
/// already works out what can be set aside each month and produces a Save More Tomorrow ramp, and all
/// that is missing is running that schedule forward until it clears the target.
/// <para>
/// No investment return is assumed. The app records no price history — only a current price and the
/// previous close — so there is no return series to estimate a growth rate from, and a compounding
/// assumption here would be a number invented to make the date look better. Contributions accumulate
/// at face value, which understates a portfolio that does earn a return, in the direction a savings
/// plan should err.
/// </para>
/// </remarks>
public static class GoalProjector
{
    /// <summary>Same base currency the rest of the app reports in.</summary>
    public const string BaseCurrency = "IDR";

    /// <param name="contributionRamp">
    /// What is set aside each month, index 0 being next month. The last value repeats once the ramp
    /// runs out, which is what a Save More Tomorrow escalation does when it reaches its target.
    /// </param>
    /// <param name="maxMonths">Bound on the search, so a contribution too small to ever arrive terminates.</param>
    public static GoalProjectionDto Project(
        string goalName,
        decimal targetAmount,
        decimal currentAmount,
        IReadOnlyList<decimal> contributionRamp,
        DateOnly today,
        int maxMonths = 600)
    {
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var target = Math.Max(0m, targetAmount);
        var current = Math.Max(0m, currentAmount);
        var steady = contributionRamp.Count > 0 ? contributionRamp[^1] : 0m;

        if (target <= 0m)
        {
            return new GoalProjectionDto(
                IsReachable: false, BaseCurrency, goalName, 0m, Round(current), 0m, Round(steady),
                null, null, 0m, [],
                ["Target belum ditentukan, jadi belum ada yang bisa diproyeksikan."]);
        }

        if (current >= target)
        {
            return new GoalProjectionDto(
                IsReachable: true, BaseCurrency, goalName, Round(target), Round(current), 0m,
                Round(steady), 0, monthStart, 100m, [],
                [$"Target {ChatFormat.Idr(target)} sudah tercapai — yang tersisa mempertahankannya."]);
        }

        var trajectory = new List<GoalMilestoneDto>();
        var balance = current;
        int? reachedAt = null;

        for (var month = 1; month <= maxMonths; month++)
        {
            var contribution = contributionRamp.Count > 0
                ? contributionRamp[Math.Min(month - 1, contributionRamp.Count - 1)]
                : 0m;

            balance += contribution;

            // Recording every month of a fifty-year run would bury the answer; the path is kept
            // only up to the month it lands, or the first five years, whichever comes first.
            if (month <= 60 || reachedAt is null)
            {
                trajectory.Add(new GoalMilestoneDto(
                    MonthOffset: month,
                    Month: monthStart.AddMonths(month),
                    Contribution: Round(contribution),
                    Balance: Round(balance),
                    ProgressShare: Share(balance, target)));
            }

            if (balance >= target)
            {
                reachedAt = month;
                break;
            }

            // Nothing coming in and nothing left to come: the balance will not move again.
            if (contribution <= 0m && month >= contributionRamp.Count && steady <= 0m) break;
        }

        var shortfall = Math.Max(0m, target - current);

        return new GoalProjectionDto(
            IsReachable: reachedAt is not null,
            BaseCurrency: BaseCurrency,
            GoalName: goalName,
            TargetAmount: Round(target),
            CurrentAmount: Round(current),
            Shortfall: Round(shortfall),
            MonthlyContribution: Round(steady),
            MonthsToTarget: reachedAt,
            TargetMonth: reachedAt is { } m ? monthStart.AddMonths(m) : null,
            ProgressShare: Share(current, target),
            Trajectory: trajectory,
            Notes: BuildNotes(goalName, target, current, shortfall, steady, reachedAt, maxMonths));
    }

    private static List<string> BuildNotes(
        string goalName, decimal target, decimal current, decimal shortfall,
        decimal steady, int? reachedAt, int maxMonths)
    {
        var notes = new List<string>();

        if (reachedAt is null)
        {
            notes.Add(steady <= 0m
                ? $"Belum ada dana yang disisihkan tiap bulan, jadi {goalName} tidak punya tanggal " +
                  $"pencapaian. Kekurangannya {ChatFormat.Idr(shortfall)}."
                : $"Dengan setoran {ChatFormat.Idr(steady)} per bulan, {goalName} belum tercapai dalam " +
                  $"{maxMonths} bulan. Naikkan setoran atau turunkan targetnya.");
        }
        else
        {
            notes.Add(
                $"Kekurangan {ChatFormat.Idr(shortfall)} ditutup dalam {reachedAt} bulan pada setoran " +
                $"{ChatFormat.Idr(steady)} per bulan.");
        }

        notes.Add(
            "Proyeksi ini tidak mengasumsikan imbal hasil investasi sama sekali — aplikasi belum " +
            "menyimpan riwayat harga, jadi tidak ada dasar untuk menebak pertumbuhan. Kalau dana ini " +
            "diinvestasikan dan berkembang, tanggalnya akan lebih cepat dari yang tertulis.");

        return notes;
    }

    private static decimal Share(decimal part, decimal whole) =>
        whole <= 0m ? 0m : Math.Round(Math.Min(part / whole, 1m) * 100m, 2);

    private static decimal Round(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero);
}
