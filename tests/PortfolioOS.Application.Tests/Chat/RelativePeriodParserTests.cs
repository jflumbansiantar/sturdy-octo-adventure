using FluentAssertions;
using PortfolioOS.Application.Chat.Slots;

namespace PortfolioOS.Application.Tests.Chat;

public class RelativePeriodParserTests
{
    // A Wednesday in the middle of a month, so week and month boundaries are both non-trivial.
    private static readonly DateOnly Today = new(2026, 9, 16);

    [Fact]
    public void Bulan_lalu_covers_the_whole_previous_month()
    {
        var period = RelativePeriodParser.Parse("berapa pengeluaran saya bulan lalu", Today);

        period.Should().NotBeNull();
        period!.From.Should().Be(new DateOnly(2026, 8, 1));
        period.To.Should().Be(new DateOnly(2026, 8, 31));
    }

    [Fact]
    public void Bulan_ini_runs_from_the_first_to_today_not_to_month_end()
    {
        // Counting spending to a future date would be wrong; the month is not over yet.
        var period = RelativePeriodParser.Parse("total belanja bulan ini", Today);

        period!.From.Should().Be(new DateOnly(2026, 9, 1));
        period.To.Should().Be(Today);
    }

    [Fact]
    public void Minggu_ini_starts_on_monday()
    {
        var period = RelativePeriodParser.Parse("pengeluaran minggu ini", Today);

        period!.From.Should().Be(new DateOnly(2026, 9, 14));   // Monday
        period.To.Should().Be(Today);
    }

    [Fact]
    public void Minggu_lalu_is_the_full_previous_monday_to_sunday()
    {
        var period = RelativePeriodParser.Parse("belanja minggu lalu", Today);

        period!.From.Should().Be(new DateOnly(2026, 9, 7));
        period.To.Should().Be(new DateOnly(2026, 9, 13));
    }

    [Fact]
    public void Bulan_kemarin_is_not_read_as_yesterday()
    {
        // "kemarin" alone means yesterday, but "bulan kemarin" means last month - the
        // substring overlap is the trap here.
        var period = RelativePeriodParser.Parse("bulan kemarin saya habis berapa", Today);

        period!.From.Should().Be(new DateOnly(2026, 8, 1));
        period.To.Should().Be(new DateOnly(2026, 8, 31));
    }

    [Theory]
    [InlineData("3 bulan terakhir", 2026, 6, 16)]
    [InlineData("6 bulan terakhir", 2026, 3, 16)]
    public void Last_n_months_counts_back_from_today(string question, int y, int m, int d)
    {
        var period = RelativePeriodParser.Parse(question, Today);

        period!.From.Should().Be(new DateOnly(y, m, d));
        period.To.Should().Be(Today);
    }

    [Fact]
    public void Tahun_ini_is_year_to_date()
    {
        var period = RelativePeriodParser.Parse("pengeluaran saya tahun ini", Today);

        period!.From.Should().Be(new DateOnly(2026, 1, 1));
        period.To.Should().Be(Today);
    }

    [Fact]
    public void A_month_name_already_past_means_this_year()
    {
        var period = RelativePeriodParser.Parse("pengeluaran januari berapa", Today);

        period!.From.Should().Be(new DateOnly(2026, 1, 1));
        period.To.Should().Be(new DateOnly(2026, 1, 31));
    }

    [Fact]
    public void A_month_name_still_ahead_means_last_year()
    {
        // Asked in September, "desember" cannot mean a December that has not happened.
        var period = RelativePeriodParser.Parse("pengeluaran desember berapa", Today);

        period!.From.Should().Be(new DateOnly(2025, 12, 1));
        period.To.Should().Be(new DateOnly(2025, 12, 31));
    }

    [Fact]
    public void English_phrasing_is_understood_too()
    {
        var period = RelativePeriodParser.Parse("how much did I spend last month", Today);

        period!.From.Should().Be(new DateOnly(2026, 8, 1));
    }

    [Fact]
    public void No_time_expression_yields_null_rather_than_a_guess()
    {
        // Callers treat null as "no filter". Defaulting to today would silently under-report.
        RelativePeriodParser.Parse("berapa total utang saya", Today).Should().BeNull();
    }
}
