namespace PortfolioOS.Application.Chat.Intents;

/// <summary>Identifiers for every question the assistant knows how to answer.</summary>
/// <remarks>
/// Plain string constants rather than an enum: they are persisted in chat_documents.skill_id
/// and appear in API responses, so a rename should be a visible, deliberate change.
/// </remarks>
public static class SkillIds
{
    public const string PortfolioSummary = "portfolio.summary";
    public const string PortfolioTopMovers = "portfolio.top_movers";
    public const string PortfolioAllocation = "portfolio.allocation";
    public const string HoldingDetail = "holding.detail";
    public const string TransactionsSpendInPeriod = "transactions.spend_in_period";
    public const string TransactionsRecent = "transactions.recent";
    public const string TransactionsByCategory = "transactions.by_category";
    public const string DebtsTotalOutstanding = "debts.total_outstanding";
    public const string DebtsHighestInterest = "debts.highest_interest";
    public const string DebtsDueSoon = "debts.due_soon";
    public const string LedgerNetWorth = "ledger.networth";
    public const string LedgerAccountBalance = "ledger.account_balance";
    public const string MarketFx = "market.fx";
    public const string HelpCapabilities = "help.capabilities";
}

/// <summary>One skill and the ways people actually ask for it.</summary>
public sealed record IntentDefinition(
    string SkillId,
    string Description,
    string CanonicalQuestion,
    IReadOnlyList<string> Phrases);

/// <summary>
/// The curated question bank. Every phrase here is embedded once and stored as an
/// <c>IntentPhrase</c> row; matching an incoming question against them is how a question gets
/// routed without a language model.
/// </summary>
/// <remarks>
/// Quality of the whole feature is roughly proportional to the coverage of this list, so
/// phrases mix Indonesian and English, formal and colloquial ("boncos", "mencekik", "habis
/// berapa"). Keep phrasings for neighbouring skills lexically distinct - portfolio value and
/// net worth are close enough in embedding space that vague phrasings in both erode the margin
/// the router depends on.
/// </remarks>
public static class IntentCatalog
{
    public static IReadOnlyList<IntentDefinition> All { get; } =
    [
        new(SkillIds.PortfolioSummary,
            "Total nilai investasi, modal, dan untung/rugi keseluruhan.",
            "Berapa total nilai portofolio saya?",
            [
                "berapa total nilai portofolio saya",
                "nilai portfolio sekarang berapa",
                "ringkasan portofolio saya",
                "total investasi saya berapa",
                // Without these, "investasi saya worth berapa" drifts to ledger.networth:
                // the two skills sit close together in embedding space, so each needs its own
                // vocabulary ("investasi/portofolio" here, "aset dikurangi utang" there).
                "berapa nilai investasi saya sekarang",
                "investasi saya sekarang worth berapa nilainya",
                "portofolio saya untung atau rugi",
                "berapa cuan portofolio saya",
                "posisi investasi saya sekarang gimana",
                "how much is my portfolio worth",
                "portfolio summary",
                "total value of my investments",
                "am I up or down overall",
            ]),

        new(SkillIds.PortfolioTopMovers,
            "Saham yang paling banyak bergerak hari ini.",
            "Saham apa yang paling banyak bergerak hari ini?",
            [
                "saham apa yang paling banyak bergerak hari ini",
                "top movers hari ini",
                "saham saya yang naik paling tinggi hari ini",
                "yang turun paling dalam hari ini apa",
                "pergerakan harian saham saya",
                "hari ini saham apa yang paling untung",
                "biggest movers today",
                "which of my stocks moved the most today",
                "today's gainers and losers",
            ]),

        new(SkillIds.PortfolioAllocation,
            "Komposisi portofolio per pasar dan per jenis aset.",
            "Bagaimana komposisi portofolio saya?",
            [
                "bagaimana komposisi portofolio saya",
                "alokasi aset saya seperti apa",
                "berapa persen saham indonesia dibanding amerika",
                "sebaran investasi saya per jenis",
                "porsi crypto saya berapa persen",
                "breakdown portofolio per pasar",
                "asset allocation breakdown",
                "how is my portfolio split",
                "percentage in each market",
            ]),

        new(SkillIds.HoldingDetail,
            "Detail satu saham: jumlah lot, harga rata-rata, dan untung/ruginya.",
            "Bagaimana posisi BBCA saya?",
            [
                "bagaimana posisi saham ini",
                "detail kepemilikan saham saya",
                "saya punya berapa lembar saham ini",
                // Very short ticker-plus-slang questions ("BBCA gimana posisinya") carry little
                // for the embedding to hold on to, and were landing just under the margin gate.
                "gimana posisi saham saya",
                "saham ini posisinya gimana",
                "posisi saya di ticker ini",
                "harga rata-rata beli saya berapa",
                "saham ini untung berapa",
                "saham ini boncos berapa",
                "posisi saya di saham ini gimana",
                "show me my position in this stock",
                "average cost of this holding",
                "how much have I gained on this stock",
            ]),

        new(SkillIds.TransactionsSpendInPeriod,
            "Total pengeluaran pada rentang waktu tertentu.",
            "Berapa pengeluaran saya bulan lalu?",
            [
                "berapa pengeluaran saya bulan lalu",
                "total belanja bulan ini berapa",
                "bulan kemarin saya habis berapa",
                "pengeluaran saya minggu ini berapa",
                "saya menghabiskan berapa tahun ini",
                "berapa duit yang keluar bulan ini",
                "pengeluaran bulan desember berapa",
                "total pengeluaran di bulan tertentu",
                "how much did I spend last month",
                "total expenses this month",
                "my spending this year",
            ]),

        new(SkillIds.TransactionsRecent,
            "Daftar transaksi terakhir.",
            "Apa saja transaksi terakhir saya?",
            [
                "apa saja transaksi terakhir saya",
                "transaksi terbaru",
                "riwayat transaksi terakhir",
                "coba lihat transaksi terakhir saya",
                "lima transaksi terakhir",
                "show my latest transactions",
                "recent transaction history",
                "what did I buy recently",
            ]),

        new(SkillIds.TransactionsByCategory,
            "Rekap transaksi dikelompokkan per kategori.",
            "Pengeluaran saya paling besar di kategori apa?",
            [
                "pengeluaran saya paling besar di kategori apa",
                "rekap transaksi per kategori",
                "uang saya habis untuk apa saja",
                "kategori pengeluaran terbesar",
                "perbandingan pemasukan dan pengeluaran",
                "breakdown transaksi berdasarkan kategori",
                "what category do I spend the most on",
                "spending by category",
                "income versus expenses",
            ]),

        new(SkillIds.DebtsTotalOutstanding,
            "Total sisa utang yang masih harus dibayar.",
            "Berapa total utang saya?",
            [
                "berapa total utang saya",
                "saya punya hutang berapa semuanya",
                "sisa hutang keseluruhan berapa",
                "jumlah cicilan saya yang belum lunas",
                "total kewajiban saya",
                "berapa sisa pinjaman saya",
                "how much debt do I have in total",
                "total outstanding balance",
                "my remaining loan balance",
            ]),

        new(SkillIds.DebtsHighestInterest,
            "Utang dengan bunga tertinggi - yang paling mahal untuk ditunda.",
            "Utang mana yang bunganya paling tinggi?",
            [
                "utang mana yang bunganya paling tinggi",
                "pinjaman paling mahal bunganya",
                "bunga utang terbesar yang mana",
                "utang mana yang paling mencekik",
                "hutang mana yang harus saya lunasi duluan",
                "cicilan dengan bunga tertinggi",
                "which debt has the highest interest rate",
                "which loan costs me the most",
                "what should I pay off first",
            ]),

        new(SkillIds.DebtsDueSoon,
            "Tagihan yang jatuh temponya paling dekat.",
            "Tagihan apa yang jatuh tempo dalam waktu dekat?",
            [
                "tagihan apa yang jatuh tempo dalam waktu dekat",
                "cicilan apa yang harus dibayar bulan ini",
                "kapan jatuh tempo utang saya",
                "tanggal pembayaran terdekat kapan",
                "utang mana yang segera jatuh tempo",
                "what bills are due soon",
                "upcoming payment dates",
                "which debt is due next",
            ]),

        new(SkillIds.LedgerNetWorth,
            "Kekayaan bersih: total aset dikurangi total kewajiban.",
            "Berapa kekayaan bersih saya?",
            [
                "berapa kekayaan bersih saya",
                "net worth saya sekarang berapa",
                "total aset dikurangi utang berapa",
                "kekayaan bersih saya naik atau turun",
                "berapa total harta bersih saya",
                "what is my net worth",
                "assets minus liabilities",
                "total equity position",
            ]),

        new(SkillIds.LedgerAccountBalance,
            "Saldo satu akun di buku besar.",
            "Berapa saldo kas saya?",
            [
                "berapa saldo akun ini",
                "saldo kas saya berapa",
                "berapa uang di rekening bank saya",
                "saldo akun buku besar",
                "posisi saldo akun tertentu",
                "what is the balance of this account",
                "how much cash do I have",
                "ledger account balance",
            ]),

        new(SkillIds.MarketFx,
            "Kurs USD ke IDR yang dipakai untuk konversi.",
            "Berapa kurs dolar sekarang?",
            [
                "berapa kurs dolar sekarang",
                "nilai tukar usd ke rupiah berapa",
                "1 dolar berapa rupiah",
                "kurs yang dipakai sistem berapa",
                "rate usd idr hari ini",
                "what is the usd to idr rate",
                "current exchange rate",
            ]),

        new(SkillIds.HelpCapabilities,
            "Daftar pertanyaan yang bisa dijawab asisten.",
            "Kamu bisa menjawab apa saja?",
            [
                "kamu bisa menjawab apa saja",
                "kamu bisa bantu apa",
                "fitur apa saja yang tersedia",
                "contoh pertanyaan yang bisa saya tanyakan",
                "apa yang bisa kamu lakukan",
                "bantuan",
                "what can you do",
                "help",
                "list of supported questions",
            ]),
    ];

    public static IReadOnlyDictionary<string, IntentDefinition> BySkillId { get; } =
        All.ToDictionary(i => i.SkillId, StringComparer.Ordinal);
}
