using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Shared.Scanning;

/// <summary>How much the parser trusts a field. Drives the colour dot on the review screen.</summary>
public enum Confidence
{
    None,     // not found at all - the UI must ask the user
    Low,      // guessed from position or fallback rules
    Medium,   // matched a pattern, but without a confirming label
    High      // matched an explicit label ("TOTAL", "Tgl", ...)
}

public enum DocumentKind
{
    Unknown,
    Receipt,      // struk belanja        -> Expense
    Transfer,     // m-banking / e-wallet -> Expense or Income
    Payslip,      // slip gaji            -> Income
    Bill,         // tagihan / cicilan    -> Debt
    BrokerTrade   // konfirmasi broker    -> Stock
}

/// <summary>A single extracted field: the value, how sure we are, and the text it came from.</summary>
public record FieldGuess<T>(T? Value, Confidence Confidence, string? Evidence = null)
{
    public static FieldGuess<T> Missing => new(default, Confidence.None);

    // For a value-type T an unconstrained T? is still non-nullable, so a null check here would
    // always pass. Confidence is the single source of truth for "did we actually read this".
    public bool HasValue => Confidence != Confidence.None;
}

/// <summary>
/// What the scanner believes the document says. Deliberately NOT a CreateTransactionCommand:
/// nothing here is trusted enough to save without the user seeing it first.
/// </summary>
public record TransactionDraft
{
    public DocumentKind Kind { get; init; } = DocumentKind.Unknown;

    public FieldGuess<DateOnly> Date { get; init; } = FieldGuess<DateOnly>.Missing;
    public FieldGuess<TransactionCategory> Category { get; init; } = FieldGuess<TransactionCategory>.Missing;
    public FieldGuess<string> Name { get; init; } = FieldGuess<string>.Missing;
    public FieldGuess<string> Type { get; init; } = FieldGuess<string>.Missing;
    public FieldGuess<decimal> Total { get; init; } = FieldGuess<decimal>.Missing;

    // Only meaningful for DocumentKind.BrokerTrade / TransactionCategory.Stock.
    public FieldGuess<Market> Market { get; init; } = FieldGuess<Market>.Missing;
    public FieldGuess<decimal> Shares { get; init; } = FieldGuess<decimal>.Missing;
    public FieldGuess<decimal> Price { get; init; } = FieldGuess<decimal>.Missing;

    /// <summary>Raw OCR output, shown collapsed on the review screen so the user can sanity-check us.</summary>
    public string RawText { get; init; } = string.Empty;

    public IReadOnlyList<string> Warnings { get; init; } = [];
}
