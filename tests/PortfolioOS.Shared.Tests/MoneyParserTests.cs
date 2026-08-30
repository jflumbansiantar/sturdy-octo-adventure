using FluentAssertions;
using PortfolioOS.Shared.Scanning;

namespace PortfolioOS.Shared.Tests;

public class MoneyParserTests
{
    [Theory]
    // Indonesian: dot groups thousands, comma is decimal
    [InlineData("Rp 1.250.000", 1250000)]
    [InlineData("Rp1.250.000,00", 1250000)]
    [InlineData("1.250.000,50", 1250000.50)]
    [InlineData("IDR 1.250.000,-", 1250000)]
    [InlineData("Rp 5.000", 5000)]
    [InlineData("Rp 500,00", 500)]
    [InlineData("12.345,67", 12345.67)]
    // US form, which plenty of apps emit anyway
    [InlineData("1,250,000.50", 1250000.50)]
    [InlineData("$5,000.00", 5000)]
    [InlineData("1,250", 1250)]
    // no separators at all
    [InlineData("25000", 25000)]
    [InlineData("0", 0)]
    public void TryParse_HandlesBothSeparatorConventions(string input, double expected)
    {
        MoneyParser.TryParse(input, out var value).Should().BeTrue($"'{input}' is a valid amount");
        value.Should().Be((decimal)expected);
    }

    [Theory]
    // Thermal paper turns digits into look-alike letters constantly.
    [InlineData("Rp l.25O.OOO", 1250000)]
    [InlineData("Rp 5O.OOO", 50000)]
    [InlineData("2S.000", 25000)]
    public void TryParse_RepairsOcrDigitConfusion(string input, double expected)
    {
        MoneyParser.TryParse(input, out var value).Should().BeTrue();
        value.Should().Be((decimal)expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("TOTAL")]
    [InlineData("Rp")]
    public void TryParse_RejectsNonNumbers(string input)
        => MoneyParser.TryParse(input, out _).Should().BeFalse();

    [Fact]
    public void ExtractAll_IgnoresWordsMadeOfDigitLookAlikes()
    {
        // "SOLO" is all confusable letters but has no real digit - it must not become 5010.
        MoneyParser.ExtractAll("SOLO SQUARE").Should().BeEmpty();
    }

    [Fact]
    public void LastIn_TakesTheRightmostAmountOnTheRow()
    {
        // "2 x 12.500  25.000" - quantity, unit price, then the line total on the right.
        MoneyParser.LastIn("2 x 12.500   25.000").Should().Be(25000m);
    }

    [Fact]
    public void ExtractAll_FindsEveryAmountLeftToRight()
        => MoneyParser.ExtractAll("SUBTOTAL 25.000 PPN 2.750 TOTAL 27.750")
            .Should().Equal(25000m, 2750m, 27750m);
}
