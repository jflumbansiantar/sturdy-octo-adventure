using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Domain.Entities;

/// <summary>
/// One embedded row of the chat corpus. Two populations share this table: curated intent
/// phrases (seeded from code, carrying <see cref="SkillId"/>) and fact cards generated from
/// the live data. Both are searched by cosine similarity, but for different purposes.
/// </summary>
public class ChatDocument
{
    public Guid Id { get; set; }
    public ChatDocumentKind Kind { get; set; }

    /// <summary>Primary key of the record this card describes, as text. Null for intent phrases.</summary>
    public string? SourceId { get; set; }

    /// <summary>Skill this phrase routes to. Only set when <see cref="Kind"/> is IntentPhrase.</summary>
    public string? SkillId { get; set; }

    /// <summary>The exact text that was embedded; also what gets shown as the answer's source.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Hash of <see cref="Content"/>. Reindexing compares this before spending ~10ms embedding
    /// a row, which is what keeps a full reindex cheap enough to run on a timer.
    /// </summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>
    /// The embedding, kept as a plain array so this project stays dependency-free.
    /// Mapped to Postgres <c>vector</c> by ChatDocumentConfiguration.
    /// </summary>
    public float[] Embedding { get; set; } = [];

    public DateTimeOffset UpdatedAt { get; set; }
}
