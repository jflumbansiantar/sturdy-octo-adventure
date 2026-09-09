using PortfolioOS.Application.Chat.Intents;

namespace PortfolioOS.Application.Tests.Chat;

/// <summary>A question and the skill it should reach, or null when it must be refused.</summary>
public sealed record EvalCase(string? Expected, string Question);

/// <summary>
/// The questions the router is measured against.
/// </summary>
/// <remarks>
/// Every entry is held out: none of these appear verbatim in <see cref="IntentCatalog"/>, so a
/// pass means the embedding generalised rather than that the phrase was memorised. Wording is
/// deliberately colloquial ("ngegas", "jeblok", "mencekik") because that is how the questions
/// actually arrive.
/// <para>
/// The refusal set is the more important half. It is not padded with nonsense: alongside
/// obviously unrelated questions it carries the three kinds this assistant must never attempt —
/// forecasts, advice, and instructions to change data — because those are the ones a user could
/// plausibly ask and act on.
/// </para>
/// </remarks>
public static class RetrievalEvalSet
{
    public static IReadOnlyList<EvalCase> Answerable { get; } =
    [
        new(SkillIds.PortfolioSummary, "portofolio saya sekarang nilainya berapa ya"),
        new(SkillIds.PortfolioSummary, "saya sudah cuan berapa dari investasi"),
        new(SkillIds.PortfolioSummary, "total duit saya di saham ada berapa"),

        new(SkillIds.PortfolioTopMovers, "hari ini yang paling ngegas saham apa"),
        new(SkillIds.PortfolioTopMovers, "saham apa yang jeblok hari ini"),
        new(SkillIds.PortfolioTopMovers, "pergerakan saham saya hari ini gimana"),

        new(SkillIds.PortfolioAllocation, "berapa persen duit saya di crypto"),
        new(SkillIds.PortfolioAllocation, "komposisi aset saya gimana"),
        new(SkillIds.PortfolioAllocation, "saya lebih banyak di saham US atau ID"),

        new(SkillIds.HoldingDetail, "BBCA gimana posisinya"),
        new(SkillIds.HoldingDetail, "posisi NVDA saya gimana"),
        new(SkillIds.HoldingDetail, "saya rugi berapa di BBRI"),
        new(SkillIds.HoldingDetail, "harga rata-rata beli TLKM berapa"),

        new(SkillIds.TransactionsSpendInPeriod, "bulan kemarin saya habis berapa"),
        new(SkillIds.TransactionsSpendInPeriod, "pengeluaran april 2026 berapa"),
        new(SkillIds.TransactionsSpendInPeriod, "minggu ini saya belanja berapa"),
        new(SkillIds.TransactionsSpendInPeriod, "total duit keluar tahun ini berapa"),

        new(SkillIds.TransactionsRecent, "transaksi apa aja yang terakhir masuk"),
        new(SkillIds.TransactionsRecent, "coba lihat catatan transaksi paling baru"),
        new(SkillIds.TransactionsRecent, "lima transaksi terbaru apa saja"),

        new(SkillIds.TransactionsByCategory, "duit saya paling banyak habis buat apa"),
        new(SkillIds.TransactionsByCategory, "kategori mana yang paling boros"),
        new(SkillIds.TransactionsByCategory, "pemasukan saya lebih besar atau pengeluaran"),

        new(SkillIds.DebtsTotalOutstanding, "saya masih punya hutang berapa"),
        new(SkillIds.DebtsTotalOutstanding, "total kewajiban saya sekarang berapa"),
        new(SkillIds.DebtsTotalOutstanding, "berapa sisa semua pinjaman saya"),

        new(SkillIds.DebtsHighestInterest, "utang mana yang bunganya paling mencekik"),
        new(SkillIds.DebtsHighestInterest, "which loan should I clear first"),
        new(SkillIds.DebtsHighestInterest, "cicilan mana yang paling merugikan saya"),

        new(SkillIds.DebtsDueSoon, "bulan ini ada tagihan apa yang harus dibayar"),
        new(SkillIds.DebtsDueSoon, "kapan saya harus bayar cicilan berikutnya"),
        new(SkillIds.DebtsDueSoon, "tagihan terdekat apa ya"),

        new(SkillIds.LedgerNetWorth, "kekayaan bersih saya berapa sekarang"),
        new(SkillIds.LedgerNetWorth, "net worth saya positif atau negatif"),
        new(SkillIds.LedgerNetWorth, "berapa harta bersih saya setelah dikurangi utang"),

        new(SkillIds.LedgerAccountBalance, "uang kas saya tinggal berapa"),
        new(SkillIds.LedgerAccountBalance, "saldo rekening bank saya berapa"),
        new(SkillIds.LedgerAccountBalance, "berapa isi akun kas saya"),

        new(SkillIds.MarketFx, "sekarang 1 usd berapa rupiah"),
        new(SkillIds.MarketFx, "dollar lagi berapa"),
        new(SkillIds.MarketFx, "kurs yang dipakai sistem sekarang berapa"),

        new(SkillIds.HelpCapabilities, "kamu bisa apa aja sih"),
        new(SkillIds.HelpCapabilities, "aku bisa nanya apa aja ke kamu"),
        new(SkillIds.HelpCapabilities, "tolong jelaskan fungsimu"),
    ];

    public static IReadOnlyList<EvalCase> MustRefuse { get; } =
    [
        // Plainly nothing to do with this data.
        new(null, "besok hujan nggak ya di Jakarta"),
        new(null, "gimana cara bikin rendang"),
        new(null, "siapa gubernur DKI Jakarta"),
        new(null, "berapa upah minimum di Jakarta"),
        new(null, "kasih aku lelucon dong"),

        // Conversational filler: no question at all.
        new(null, "hai, selamat siang"),
        new(null, "makasih banyak ya"),
        new(null, "kamu ini sebenarnya apa"),

        // General knowledge that sounds financial but is not about this user's records.
        new(null, "inflasi itu artinya apa"),
        new(null, "reksa dana itu apa sih"),

        // Forecasts. Nothing here can predict, and a template answer would look like one.
        new(null, "BBCA bulan depan kira-kira berapa"),
        new(null, "portofolio saya bakal naik nggak tahun depan"),

        // Advice. Out of scope, and the ticker makes it a corroboration trap.
        new(null, "saya mesti beli apa ya"),
        new(null, "tolong order BBCA sekarang juga"),

        // Instructions to change data. The assistant is read-only.
        new(null, "ganti average cost BBCA jadi 5000"),
        new(null, "tolong hapus catatan transaksi bulan lalu"),
    ];
}
