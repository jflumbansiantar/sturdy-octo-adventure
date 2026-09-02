using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Application.Chat;

/// <summary>One indexed document and how close it was to the question. Score is cosine similarity.</summary>
public sealed record ScoredDocument(
    ChatDocumentKind Kind,
    string? SkillId,
    string? SourceId,
    string Content,
    double Score);

public interface IChatRetriever
{
    /// <summary>Curated question phrasings, ordered best first. Used to pick a skill.</summary>
    Task<IReadOnlyList<ScoredDocument>> SearchIntentsAsync(
        float[] queryVector, int take, CancellationToken ct = default);

    /// <summary>Fact cards describing real records, ordered best first. Used for free-text lookup.</summary>
    Task<IReadOnlyList<ScoredDocument>> SearchFactsAsync(
        float[] queryVector, int take, CancellationToken ct = default);

    /// <summary>
    /// Every card of one kind, unscored, for literal (non-embedding) matching.
    /// </summary>
    /// <remarks>
    /// Only used for small populations such as holdings. Embeddings are close to useless on
    /// short opaque tokens like "NVDA", so finding those needs plain string matching.
    /// </remarks>
    Task<IReadOnlyList<ScoredDocument>> ListAsync(
        ChatDocumentKind kind, CancellationToken ct = default);
}
