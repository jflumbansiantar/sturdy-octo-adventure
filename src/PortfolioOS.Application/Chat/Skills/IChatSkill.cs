namespace PortfolioOS.Application.Chat.Skills;

/// <summary>What a skill gets to work with once routing has chosen it.</summary>
/// <param name="Question">The user's original wording, for slot extraction.</param>
/// <param name="Today">Injected rather than read from the clock, so answers are reproducible in tests.</param>
/// <param name="Facts">Fact cards that matched the question, for skills that need to identify a record.</param>
public sealed record ChatSkillContext(
    string Question,
    DateOnly Today,
    IReadOnlyList<ScoredDocument> Facts);

/// <summary>
/// Answers one kind of question.
/// </summary>
/// <remarks>
/// Every implementation is a thin wrapper over an existing MediatR query — the point of this
/// layer is phrasing, not calculation. If a skill ever needs to compute a figure itself, that
/// figure almost certainly belongs in a query handler where the rest of the app can see it.
/// </remarks>
public interface IChatSkill
{
    string SkillId { get; }

    Task<ChatAnswer> ExecuteAsync(ChatSkillContext context, CancellationToken ct = default);
}
