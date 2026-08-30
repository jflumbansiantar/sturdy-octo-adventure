namespace PortfolioOS.Shared.Scanning.Parsers;

/// <summary>
/// Turns recognised text into a draft transaction for one kind of document.
/// Implementations must never throw and never guess silently: a field they cannot read
/// stays <see cref="FieldGuess{T}.Missing"/> so the review screen can ask the user.
/// </summary>
public interface IDocumentParser
{
    DocumentKind Kind { get; }

    TransactionDraft Parse(OcrText ocr);
}
