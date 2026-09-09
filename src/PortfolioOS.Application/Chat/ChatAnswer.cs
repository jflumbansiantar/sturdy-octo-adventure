namespace PortfolioOS.Application.Chat;

/// <summary>A record the answer was drawn from, shown so the user can check the working.</summary>
public sealed record ChatSource(string Label, string? SourceId = null);

/// <summary>Tabular detail behind an answer, rendered as a table by the client.</summary>
public sealed record ChatTable(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows);

/// <summary>
/// What the assistant says back.
/// </summary>
/// <remarks>
/// <see cref="Text"/> is composed from templates filled with figures returned by the existing
/// MediatR queries — never from anything the retrieval layer produced. Embeddings pick *which*
/// question is being asked; they never supply a number.
/// </remarks>
public sealed record ChatAnswer(
    string Text,
    string? SkillId = null,
    ChatTable? Table = null,
    IReadOnlyList<ChatSource>? Sources = null,
    double Confidence = 0,
    IReadOnlyList<string>? Suggestions = null)
{
    public IReadOnlyList<ChatSource> Sources { get; init; } = Sources ?? [];
    public IReadOnlyList<string> Suggestions { get; init; } = Suggestions ?? [];
}
