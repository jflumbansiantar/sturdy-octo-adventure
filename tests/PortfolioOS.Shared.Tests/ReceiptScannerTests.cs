using FluentAssertions;
using PortfolioOS.Domain.Enums;
using PortfolioOS.Shared.Scanning;

namespace PortfolioOS.Shared.Tests;

/// <summary>
/// End-to-end: recognised text in, draft out. One fixture per document kind the app supports.
/// </summary>
public class ReceiptScannerTests
{
    private readonly ReceiptScanner _scanner = new();

    private static OcrText Struk() => OcrFixture.Column(
        "INDOMARET KEBON JERUK",
        "JL. RAYA KEBON JERUK NO. 12",
        "Telp: 021-5566778",
        "Tgl : 21/04/2026  Kasir: ANI",
        "Aqua 600ml     2 x 4.000    8.000",
        "Indomie Goreng 3 x 3.500   10.500",
        "Roti Tawar     1 x 15.500  15.500",
        "SUBTOTAL                   34.000",
        "PPN 11%                     3.740",
        "TOTAL                      37.740",
        "TUNAI                      50.000",
        "KEMBALI                    12.260",
        "TERIMA KASIH");

    private static OcrText TransferKeluar() => OcrFixture.Column(
        "BCA mobile",
        "Transfer Berhasil",
        "21 Apr 2026 14:35",
        "Dari: JOHN DOE",
        "Penerima: BUDI SANTOSO",
        "Bank Tujuan: BCA",
        "Nominal: Rp 250.000",
        "Biaya: Rp 0",
        "No. Ref: 20260421143512");

    private static OcrText SaldoMasuk() => OcrFixture.Column(
        "GoPay",
        "Saldo Masuk",
        "Rp 500.000",
        "dari BUDI SANTOSO",
        "21 Apr 2026");

    private static OcrText SlipGaji() => OcrFixture.Column(
        "PT MAJU JAYA SENTOSA",
        "SLIP GAJI KARYAWAN",
        "Periode: 30 April 2026",
        "Nama: JOHN DOE",
        "Gaji Pokok            8.000.000",
        "Tunjangan Transport   1.000.000",
        "Total Penerimaan      9.000.000",
        "Potongan BPJS           320.000",
        "PPh 21                  250.000",
        "Take Home Pay         8.430.000");

    private static OcrText TagihanKartuKredit() => OcrFixture.Column(
        "Bank BCA",
        "Tagihan Kartu Kredit",
        "Periode: 01 Apr 2026 - 30 Apr 2026",
        "Total Tagihan       Rp 4.750.000",
        "Pembayaran Minimum  Rp   475.000",
        "Jatuh Tempo         25/05/2026",
        "Limit Kredit        Rp 20.000.000");

    private static OcrText KonfirmasiBeliSaham() => OcrFixture.Column(
        "Stockbit",
        "Order Confirmation",
        "BELI BBCA",
        "21 Apr 2026 09:15",
        "Jumlah: 10 Lot",
        "Harga: Rp 9.850",
        "Nilai: Rp 9.850.000",
        "Fee Beli: Rp 14.775",
        "Total: Rp 9.864.775");

    [Fact]
    public void Struk_BecomesAnExpense()
    {
        var draft = _scanner.Scan(Struk());

        draft.Kind.Should().Be(DocumentKind.Receipt);
        draft.Category.Value.Should().Be(TransactionCategory.Expense);
        draft.Type.Value.Should().Be("Debit");
        draft.Total.Value.Should().Be(37740m, "TOTAL is the bill, not TUNAI");
        draft.Date.Value.Should().Be(new DateOnly(2026, 4, 21));
        draft.Name.Value.Should().Be("INDOMARET KEBON JERUK");
    }

    [Fact]
    public void TransferKeluar_BecomesAnExpenseNamedAfterTheRecipient()
    {
        var draft = _scanner.Scan(TransferKeluar());

        draft.Kind.Should().Be(DocumentKind.Transfer);
        draft.Category.Value.Should().Be(TransactionCategory.Expense);
        draft.Category.Confidence.Should().Be(Confidence.High);
        draft.Total.Value.Should().Be(250000m);
        draft.Name.Value.Should().Be("BUDI SANTOSO");
        draft.Date.Value.Should().Be(new DateOnly(2026, 4, 21));
    }

    [Fact]
    public void SaldoMasuk_BecomesIncome()
    {
        var draft = _scanner.Scan(SaldoMasuk());

        draft.Category.Value.Should().Be(TransactionCategory.Income);
        draft.Type.Value.Should().Be("Credit");
        draft.Total.Value.Should().Be(500000m);
    }

    [Fact]
    public void SlipGaji_UsesTakeHomePayNotGross()
    {
        var draft = _scanner.Scan(SlipGaji());

        draft.Kind.Should().Be(DocumentKind.Payslip);
        draft.Category.Value.Should().Be(TransactionCategory.Income);
        draft.Total.Value.Should().Be(8430000m, "gross pay of 9.000.000 is the wrong number");
        draft.Date.Value.Should().Be(new DateOnly(2026, 4, 30));
    }

    [Fact]
    public void Tagihan_BecomesDebtAndAvoidsTheDueDate()
    {
        var draft = _scanner.Scan(TagihanKartuKredit());

        draft.Kind.Should().Be(DocumentKind.Bill);
        draft.Category.Value.Should().Be(TransactionCategory.Debt);
        draft.Total.Value.Should().Be(4750000m, "the minimum payment is not the bill");
        draft.Date.Value.Should().Be(new DateOnly(2026, 4, 1), "25/05/2026 is when it is due, not when it happened");
    }

    [Fact]
    public void KonfirmasiBroker_FillsEveryFieldTheStockValidatorDemands()
    {
        var draft = _scanner.Scan(KonfirmasiBeliSaham());

        draft.Kind.Should().Be(DocumentKind.BrokerTrade);
        draft.Category.Value.Should().Be(TransactionCategory.Stock);
        draft.Type.Value.Should().Be("BUY");
        draft.Name.Value.Should().Be("BBCA", "CreateTransactionHandler matches the holding by ticker");
        draft.Shares.Value.Should().Be(1000m, "10 lot is 1000 shares on IDX");
        draft.Price.Value.Should().Be(9850m);
        draft.Market.Value.Should().Be(Market.ID);
        draft.Total.Value.Should().Be(9864775m);
    }

    [Fact]
    public void UnknownDocument_IsReadAsAReceiptButFlagged()
    {
        var draft = _scanner.Scan(OcrFixture.Column("CATATAN", "Beli bensin", "150.000"));

        draft.Category.Confidence.Should().Be(Confidence.Low);
        draft.Warnings.Should().Contain(w => w.Contains("tidak dikenali"));
    }

    [Fact]
    public void EmptyScan_ReturnsNothingAndSaysWhy()
    {
        var draft = _scanner.Scan(OcrText.Empty);

        draft.Total.HasValue.Should().BeFalse();
        draft.Warnings.Should().ContainSingle().Which.Should().Contain("Tidak ada teks");
    }

    [Fact]
    public void StockDraftMissingItsNumbers_WarnsBeforeTheApiWouldReject()
    {
        // Only enough to classify as a trade - no price, no quantity.
        var draft = _scanner.Scan(OcrFixture.Column("Stockbit", "Kode Saham: BBRI", "sekuritas"));

        draft.Category.Value.Should().Be(TransactionCategory.Stock);
        draft.Warnings.Should().Contain(w => w.Contains("Market, Lembar, dan Harga"));
    }
}
