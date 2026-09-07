namespace PortfolioOS.Web.Services;

/// <summary>
/// Hand-off between the floating assistant button and the chat page.
///
/// The button sits in the layout, so it can be pressed from any page - but the answer belongs
/// on /chat, where the conversation lives. The picked question is parked here, the button
/// navigates, and the chat page reads it back on its first render and asks it.
/// </summary>
public class AssistantState
{
    /// <summary>
    /// The quick questions offered by the floating button and by the empty chat log. Kept in
    /// one place so the two lists cannot drift apart.
    /// </summary>
    public static readonly string[] Starters =
    [
        "Berapa total nilai portofolio saya?",
        "Utang mana yang bunganya paling tinggi?",
        "Berapa pengeluaran saya bulan lalu?",
        "Berapa kekayaan bersih saya?",
    ];

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
}
