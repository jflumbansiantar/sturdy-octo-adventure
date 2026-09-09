namespace PortfolioOS.Application.Common.Interfaces;

/// <summary>
/// Which side of the retrieval pair a text sits on. The E5 model family was trained with these
/// as literal prefixes and quietly loses accuracy without them.
/// </summary>
/// <remarks>
/// Not merely cosmetic, and not a straight query/document split. Measured on this corpus:
/// embedding intent phrases as <see cref="Query"/> rather than <see cref="Passage"/> roughly
/// doubled the margin between the correct skill and the runner-up (0.058 -> 0.092), because
/// matching a question against a question is a symmetric comparison. Fact cards are genuine
/// documents and stay <see cref="Passage"/>.
/// </remarks>
public enum EmbeddingKind
{
    Query,
    Passage
}

public interface IEmbeddingService
{
    /// <summary>Length of every vector this service produces. Must match the DB column width.</summary>
    int Dimensions { get; }

    Task<float[]> EmbedAsync(string text, EmbeddingKind kind, CancellationToken ct = default);

    Task<IReadOnlyList<float[]>> EmbedManyAsync(
        IReadOnlyList<string> texts, EmbeddingKind kind, CancellationToken ct = default);
}
