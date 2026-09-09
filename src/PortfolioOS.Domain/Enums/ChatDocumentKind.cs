namespace PortfolioOS.Domain.Enums;

/// <summary>
/// What a row in the chat index actually is. The distinction matters at query time:
/// <see cref="IntentPhrase"/> rows are matched to route a question to a skill, everything
/// else is matched to surface a specific record.
/// </summary>
public enum ChatDocumentKind
{
    /// <summary>A curated phrasing of a question, carrying the skill it should route to.</summary>
    IntentPhrase,

    Holding,
    Debt,
    Transaction,
    JournalEntry,
    LedgerAccount,

    /// <summary>Static help text describing what the assistant can answer.</summary>
    HelpTopic
}
