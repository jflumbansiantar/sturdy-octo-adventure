using FluentAssertions;
using PortfolioOS.Shared.Scanning;

namespace PortfolioOS.Shared.Tests;

public class AmountPickerTests
{
    [Fact]
    public void FindTotal_IgnoresCashTenderedAndChange()
    {
        // The whole reason this class exists: TUNAI (50.000) is the largest number on the
        // receipt, and SUBTOTAL sits directly above the real total.
        var ocr = OcrFixture.Column(
            "INDOMARET KEBON JERUK",
            "SUBTOTAL          34.000",
            "PPN 11%            3.740",
            "TOTAL             37.740",
            "TUNAI             50.000",
            "KEMBALI           12.260");

        var total = AmountPicker.FindTotal(ocr);

        total.Value.Should().Be(37740m);
        total.Confidence.Should().Be(Confidence.Medium);
    }

    [Fact]
    public void FindTotal_PrefersGrandTotalOverAPlainTotal()
    {
        var ocr = OcrFixture.Column(
            "TOTAL             37.740",
            "DISKON             2.740",
            "GRAND TOTAL       35.000");

        var total = AmountPicker.FindTotal(ocr);

        total.Value.Should().Be(35000m);
        total.Confidence.Should().Be(Confidence.High);
    }

    [Fact]
    public void FindTotal_ReadsTheValueFromTheRightHandColumn()
    {
        // OCR emits the label and the amount as two separate lines on the same row.
        var ocr = OcrFixture.Rows(
            ("SUBTOTAL", "34.000"),
            ("TOTAL BAYAR", "37.740"),
            ("TUNAI", "50.000"));

        AmountPicker.FindTotal(ocr).Value.Should().Be(37740m);
    }

    [Fact]
    public void FindTotal_DoesNotMistakeAnItemCountForMoney()
    {
        var ocr = OcrFixture.Column(
            "TOTAL ITEM             3",
            "TOTAL QTY              7",
            "TOTAL             27.500");

        AmountPicker.FindTotal(ocr).Value.Should().Be(27500m);
    }

    [Fact]
    public void FindTotal_FallsBackToTheLargestAmountButSaysSo()
    {
        // E-wallet screenshots often show the amount alone, with no label at all.
        var ocr = OcrFixture.Column("GoPay", "Rp 25.000", "Berhasil");

        var total = AmountPicker.FindTotal(ocr);

        total.Value.Should().Be(25000m);
        total.Confidence.Should().Be(Confidence.Low, "an unlabelled guess must be flagged for review");
    }

    [Fact]
    public void ForLabel_ReadsTheAmountBelongingToAnArbitraryLabel()
    {
        var ocr = OcrFixture.Rows(
            ("Harga", "Rp 9.850"),
            ("Nilai", "Rp 9.850.000"));

        AmountPicker.ForLabel(ocr, @"\bharga\b").Value.Should().Be(9850m);
    }

    [Fact]
    public void FindTotal_ReturnsMissingWhenThereAreNoNumbersAtAll()
        => AmountPicker.FindTotal(OcrFixture.Column("TERIMA KASIH", "SELAMAT DATANG"))
            .HasValue.Should().BeFalse();
}
