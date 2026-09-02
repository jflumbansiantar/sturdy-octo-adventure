using PortfolioOS.Application.Chat.Intents;

namespace PortfolioOS.Application.Chat;

/// <summary>
/// The outcome of routing. <see cref="SkillId"/> is null when the question was refused, in
/// which case <see cref="Suggestions"/> carries what to offer the user instead.
/// </summary>
public sealed record RoutingDecision(
    string? SkillId,
    double Score,
    double Margin,
    IReadOnlyList<string> Suggestions)
{
    public bool Accepted => SkillId is not null;
}

/// <summary>
/// Turns a ranked list of intent phrases into an accept-or-refuse decision.
/// </summary>
/// <remarks>
/// Deliberately a pure function over already-scored candidates, with no database and no model,
/// so the gate that decides whether the assistant speaks at all is directly unit-testable.
/// <para>
/// Refusing is a first-class outcome, not an error path. Without a language model there is
/// nothing to gracefully waffle with, so a question outside the catalogue must produce
/// suggestions rather than the nearest-but-wrong answer.
/// </para>
/// </remarks>
public static class IntentRouter
{
    /// <param name="corroborated">
    /// Skills for which the question carries independent, non-embedding evidence — a ticker the
    /// user actually holds, a date expression. When the winning skill is one of these, the
    /// thresholds relax, because two unrelated signals agreeing is stronger proof than either
    /// alone. Evidence for a skill that did *not* win changes nothing: "tolong belikan saham
    /// BBCA" names a real ticker but is a trade instruction, and must still be refused.
    /// </param>
    public static RoutingDecision Route(
        IReadOnlyList<ScoredDocument> candidates,
        IReadOnlyCollection<string>? corroborated = null)
    {
        if (candidates.Count == 0)
            return new RoutingDecision(null, 0, 0, DefaultSuggestions());

        var top = candidates[0];
        var topSkill = top.SkillId;

        if (topSkill is null)
            return new RoutingDecision(null, top.Score, 0, DefaultSuggestions());

        // Margin is measured against the best phrase of a *different* skill. Comparing against
        // candidates[1] would be meaningless: the runner-up is usually another phrasing of the
        // same skill, which says nothing about whether the choice was clear-cut.
        var bestOther = candidates.FirstOrDefault(c => c.SkillId is not null && c.SkillId != topSkill);
        var margin = bestOther is null ? 1.0 : top.Score - bestOther.Score;

        var hasEvidence = corroborated?.Contains(topSkill) == true;
        var minScore = hasEvidence ? ChatDefaults.CorroboratedMinScore : ChatDefaults.MinScore;
        var minMargin = hasEvidence ? ChatDefaults.CorroboratedMinMargin : ChatDefaults.MinMargin;

        var accepted = top.Score >= minScore && margin >= minMargin;

        return new RoutingDecision(
            accepted ? topSkill : null,
            top.Score,
            margin,
            accepted ? [] : SuggestionsFrom(candidates));
    }

    /// <summary>The nearest questions we *can* answer, so a refusal still moves the user forward.</summary>
    private static IReadOnlyList<string> SuggestionsFrom(IReadOnlyList<ScoredDocument> candidates)
    {
        var suggestions = candidates
            .Where(c => c.SkillId is not null)
            .Select(c => c.SkillId!)
            .Distinct(StringComparer.Ordinal)
            .Take(ChatDefaults.SuggestionCount)
            .Where(IntentCatalog.BySkillId.ContainsKey)
            .Select(id => IntentCatalog.BySkillId[id].CanonicalQuestion)
            .ToList();

        return suggestions.Count > 0 ? suggestions : DefaultSuggestions();
    }

    private static IReadOnlyList<string> DefaultSuggestions() =>
    [
        IntentCatalog.BySkillId[SkillIds.PortfolioSummary].CanonicalQuestion,
        IntentCatalog.BySkillId[SkillIds.DebtsHighestInterest].CanonicalQuestion,
        IntentCatalog.BySkillId[SkillIds.TransactionsSpendInPeriod].CanonicalQuestion,
    ];
}
