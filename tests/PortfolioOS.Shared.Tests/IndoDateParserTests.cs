using FluentAssertions;
using PortfolioOS.Shared.Scanning;

namespace PortfolioOS.Shared.Tests;

public class IndoDateParserTests
{
    [Theory]
    [InlineData("21/04/2026", 2026, 4, 21)]
    [InlineData("21-04-2026", 2026, 4, 21)]
    [InlineData("21.04.2026", 2026, 4, 21)]
    [InlineData("21/04/26", 2026, 4, 21)]
    [InlineData("2026-04-21", 2026, 4, 21)]
    [InlineData("21 Apr 2026", 2026, 4, 21)]
    [InlineData("21 April 2026", 2026, 4, 21)]
    [InlineData("15 Agt 2026", 2026, 8, 15)]
    [InlineData("15 Agustus 2026", 2026, 8, 15)]
    [InlineData("3 Mei 2026", 2026, 5, 3)]
    [InlineData("9 Okt 2026", 2026, 10, 9)]
    [InlineData("25 Des 2026", 2026, 12, 25)]
    public void ParseLine_ReadsTheCommonFormats(string input, int y, int m, int d)
        => IndoDateParser.ParseLine(input).Value.Should().Be(new DateOnly(y, m, d));

    [Fact]
    public void ParseLine_AssumesDayFirst_NotMonthFirst()
    {
        // The whole point: 04/03 is 4 March in Indonesia, not 3 April.
        var guess = IndoDateParser.ParseLine("04/03/2026");
        guess.Value.Should().Be(new DateOnly(2026, 3, 4));
        guess.Confidence.Should().Be(Confidence.Low, "the digits alone cannot settle it - the user must check");
    }

    [Fact]
    public void ParseLine_IsConfidentWhenTheDayCannotBeAMonth()
        => IndoDateParser.ParseLine("21/04/2026").Confidence.Should().Be(Confidence.Medium);

    [Fact]
    public void ParseLine_FlipsWhenTheSecondComponentCannotBeAMonth()
        => IndoDateParser.ParseLine("04/21/2026").Value.Should().Be(new DateOnly(2026, 4, 21));

    [Theory]
    [InlineData("TOTAL 1.250.000")]     // money must not be mistaken for a date
    [InlineData("Jam 14:35")]
    [InlineData("")]
    [InlineData("STRUK BELANJA")]
    public void ParseLine_FindsNothingWhereThereIsNoDate(string input)
        => IndoDateParser.ParseLine(input).HasValue.Should().BeFalse();

    [Fact]
    public void Find_PrefersTheLabelledDateOverAnyOtherOnThePage()
    {
        var ocr = new OcrText("", [
            new OcrLine("Berlaku s/d 31/12/2027", 0, 10, 200, 20),
            new OcrLine("Tgl : 21/04/2026", 0, 40, 200, 20)
        ]);

        var guess = IndoDateParser.Find(ocr);

        guess.Value.Should().Be(new DateOnly(2026, 4, 21));
        guess.Confidence.Should().Be(Confidence.High);
    }
}
