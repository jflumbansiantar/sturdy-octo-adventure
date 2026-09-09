using PortfolioOS.Web.Models;

namespace PortfolioOS.Web.Services;

/// <summary>
/// The assistant's state, held outside the chat page so it survives navigation.
///
/// Two things live here. The conversation itself, because the floating button turned /chat
/// into somewhere users pass through rather than sit - a page-local list would be emptied by
/// every trip back to the dashboard. And the hand-off for a question picked from that button:
/// it is parked here, the button navigates, and the chat page asks it on arrival.
/// </summary>
public class AssistantState
{
    /// <summary>
    /// Shown when there is no better reason to show anything else - the chat page's empty log,
    /// and pages with no questions of their own.
    /// </summary>
    public static readonly string[] Starters =
    [
        "Berapa total nilai portofolio saya?",
        "Utang mana yang bunganya paling tinggi?",
        "Berapa pengeluaran saya bulan lalu?",
        "Berapa kekayaan bersih saya?",
    ];

    /// <summary>
    /// What to offer on each page. Someone looking at their debts is far likelier to want a
    /// debt question than the same four questions the dashboard offers.
    /// </summary>
    /// <remarks>
    /// Every string is copied verbatim from an <c>IntentDefinition.CanonicalQuestion</c> in the
    /// API's IntentCatalog, so each one is guaranteed to clear the router's score gate. The
    /// catalogue is not exposed over HTTP, so this is a copy rather than a fetch - the same
    /// trade this client already makes for its view models. Reword one of these only by
    /// copying the API's new wording, or the question stops routing.
    /// </remarks>
    private static readonly Dictionary<string, string[]> ByRoute = new(StringComparer.OrdinalIgnoreCase)
    {
        [""] = Starters,   // dashboard: a spread across all four areas
        ["holdings"] =
        [
            "Berapa total nilai portofolio saya?",
            "Bagaimana komposisi portofolio saya?",
            "Saham apa yang paling banyak bergerak hari ini?",
            "Berapa kekayaan bersih saya?",
        ],
        ["transactions"] =
        [
            "Berapa pengeluaran saya bulan lalu?",
            "Pengeluaran saya paling besar di kategori apa?",
            "Apa saja transaksi terakhir saya?",
            "Berapa kekayaan bersih saya?",
        ],
        ["market"] =
        [
            "Berapa kurs dolar sekarang?",
            "Saham apa yang paling banyak bergerak hari ini?",
            "Berapa total nilai portofolio saya?",
            "Bagaimana komposisi portofolio saya?",
        ],
        ["ledger"] =
        [
            "Berapa kekayaan bersih saya?",
            "Berapa saldo kas saya?",
            "Pengeluaran saya paling besar di kategori apa?",
            "Berapa total utang saya?",
        ],
        ["debts"] =
        [
            "Berapa total utang saya?",
            "Utang mana yang bunganya paling tinggi?",
            "Tagihan apa yang jatuh tempo dalam waktu dekat?",
            "Berapa kekayaan bersih saya?",
        ],
        ["savings"] =
        [
            "Berapa pengeluaran saya bulan lalu?",
            "Pengeluaran saya paling besar di kategori apa?",
            "Berapa saldo kas saya?",
            "Berapa kekayaan bersih saya?",
        ],
    };

    /// <param name="route">Path relative to the app base, without leading slash or query.</param>
    public static IReadOnlyList<string> StartersFor(string route) =>
        ByRoute.TryGetValue(route, out var starters) ? starters : Starters;

    /// <summary>
    /// The conversation on screen. Public and mutable because the chat page owns the asking -
    /// this class only outlives it.
    /// </summary>
    public List<ChatMessage> Messages { get; } = [];

    private string? _pending;

    /// <summary>Parks a question for the chat page. The caller navigates there itself.</summary>
    public void Queue(string question) => _pending = question;

    /// <summary>
    /// Returns the parked question once and forgets it. Without the forgetting, every later
    /// visit to /chat would re-ask whatever was picked the first time.
    /// </summary>
    public string? TakePending()
    {
        var question = _pending;
        _pending = null;
        return question;
    }

    /// <summary>Starts over. A conversation that outlives the page needs a way to end.</summary>
    public void Clear()
    {
        Messages.Clear();
        _pending = null;
    }
}
