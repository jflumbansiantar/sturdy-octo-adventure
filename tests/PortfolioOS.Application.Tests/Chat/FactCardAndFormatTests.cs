using FluentAssertions;
using PortfolioOS.Application.Chat;
using PortfolioOS.Application.Chat.Skills;
using PortfolioOS.Domain.Entities;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Application.Tests.Chat;

public class FactCardBuilderTests
{
    [Fact]
    public void Holding_card_carries_the_terms_someone_would_search_for()
    {
        var card = FactCardBuilder.ForHolding(new Holding
        {
            Ticker = "BBCA",
            Name = "Bank Central Asia",
            Type = HoldingType.Stock,
            Market = Market.ID,
            Shares = 500,
            AvgCost = 9100,
        });

        card.Should().Contain("BBCA").And.Contain("Bank Central Asia");
        card.Should().Contain("pasar Indonesia");
        card.Should().Contain("Rp");
    }

    [Fact]
    public void Us_holding_prices_are_shown_in_dollars_not_rupiah()
    {
        // A per-unit price belongs to the exchange it trades on; rendering NVDA's cost basis
        // as rupiah would be a currency bug, not a formatting preference.
        var card = FactCardBuilder.ForHolding(new Holding
        {
            Ticker = "NVDA", Name = "NVIDIA", Type = HoldingType.Stock,
            Market = Market.US, Shares = 10, AvgCost = 120.50m,
        });

        card.Should().Contain("USD").And.NotContain("Rp");
    }

    [Fact]
    public void Debt_card_includes_notes_so_free_text_search_can_find_them()
    {
        var card = FactCardBuilder.ForDebt(new Debt
        {
            Name = "Kartu Kredit BCA", Type = DebtType.CreditCard, Balance = 5_200_000,
            InterestRate = 27m, MinimumPayment = 500_000, DueDay = 12,
            Currency = CurrencyType.IDR, Status = DebtStatus.Active,
            DebtApp = "myBCA", Notes = "cicilan laptop kerja",
        });

        card.Should().Contain("kartu kredit");
        card.Should().Contain("cicilan laptop kerja");
        card.Should().Contain("myBCA");
    }

    [Fact]
    public void Transaction_card_states_the_date_in_words()
    {
        var card = FactCardBuilder.ForTransaction(new Transaction
        {
            Date = new DateOnly(2026, 4, 21),
            Category = TransactionCategory.Expense,
            Name = "INDOMARET KEBON JERUK",
            Type = "Debit",
            Total = 37_740,
        });

        card.Should().Contain("INDOMARET KEBON JERUK").And.Contain("2026");
    }
}

public class ChatFormatTests
{
    [Fact]
    public void A_change_carries_its_sign()
    {
        ChatFormat.Pct(21.23m).Should().StartWith("+");
        ChatFormat.Pct(-33.54m).Should().StartWith("-");
    }

    [Fact]
    public void A_rate_does_not_carry_a_sign()
    {
        // "+27,00% per tahun" reads as if the rate rose by 27 points.
        ChatFormat.Rate(27m).Should().NotStartWith("+").And.Contain("27");
    }

    [Fact]
    public void A_loss_is_rendered_as_negative_money()
    {
        ChatFormat.SignedIdr(-3_220_000m).Should().StartWith("-Rp");
    }

    [Fact]
    public void Money_follows_the_currency_it_is_given()
    {
        ChatFormat.Money(120.5m, "USD").Should().StartWith("USD");
        ChatFormat.Money(120.5m, "IDR").Should().StartWith("Rp");
        ChatFormat.Money(120.5m, null).Should().StartWith("Rp");
    }
}

public class DueDateTests
{
    [Fact]
    public void A_due_day_still_ahead_this_month_is_days_away()
    {
        DebtsDueSoonSkill.DaysUntil(20, new DateOnly(2026, 9, 16)).Should().Be(4);
    }

    [Fact]
    public void A_due_day_already_past_rolls_into_next_month()
    {
        DebtsDueSoonSkill.DaysUntil(5, new DateOnly(2026, 9, 16)).Should().Be(19);
    }

    [Fact]
    public void Today_is_zero_days_away()
    {
        DebtsDueSoonSkill.DaysUntil(16, new DateOnly(2026, 9, 16)).Should().Be(0);
    }

    [Fact]
    public void Day_31_is_clamped_to_the_end_of_a_short_month()
    {
        // September has 30 days; a 31st due date falls on the 30th rather than overflowing.
        DebtsDueSoonSkill.DaysUntil(31, new DateOnly(2026, 9, 16)).Should().Be(14);
    }
}
