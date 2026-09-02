using FluentAssertions;
using PortfolioOS.Application.Chat;
using PortfolioOS.Application.Chat.Intents;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Application.Tests.Chat;

/// <summary>
/// Tests the gate that decides whether the assistant answers at all.
/// </summary>
/// <remarks>
/// No embedding model and no database: <see cref="IntentRouter"/> takes already-scored
/// candidates, so the decision logic can be driven with exact numbers. The scores used here
/// are the ones actually measured against the real catalogue, so the thresholds are pinned to
/// observed behaviour rather than to invented values.
/// </remarks>
public class IntentRoutingTests
{
    private static ScoredDocument Phrase(string skillId, double score) =>
        new(ChatDocumentKind.IntentPhrase, skillId, null, $"phrase for {skillId}", score);

    [Fact]
    public void Accepts_a_clear_match()
    {
        var decision = IntentRouter.Route([
            Phrase(SkillIds.DebtsHighestInterest, 0.9599),
            Phrase(SkillIds.DebtsHighestInterest, 0.9200),
            Phrase(SkillIds.DebtsTotalOutstanding, 0.8717),
        ]);

        decision.Accepted.Should().BeTrue();
        decision.SkillId.Should().Be(SkillIds.DebtsHighestInterest);
        decision.Suggestions.Should().BeEmpty();
    }

    [Fact]
    public void Rejects_when_the_best_score_is_too_low()
    {
        // "resep nasi goreng pedas" scored 0.8429 against the real catalogue.
        var decision = IntentRouter.Route([
            Phrase(SkillIds.HelpCapabilities, 0.8429),
            Phrase(SkillIds.PortfolioSummary, 0.8385),
        ]);

        decision.Accepted.Should().BeFalse();
        decision.SkillId.Should().BeNull();
        decision.Suggestions.Should().NotBeEmpty();
    }

    [Fact]
    public void Rejects_when_two_skills_are_too_close_to_separate()
    {
        // High score, but nothing distinguishes the top two skills - the case an absolute
        // threshold alone cannot catch.
        var decision = IntentRouter.Route([
            Phrase(SkillIds.PortfolioSummary, 0.9500),
            Phrase(SkillIds.LedgerNetWorth, 0.9480),
        ]);

        decision.Accepted.Should().BeFalse();
        decision.Margin.Should().BeLessThan(ChatDefaults.MinMargin);
    }

    [Fact]
    public void Margin_ignores_further_phrasings_of_the_same_skill()
    {
        // The runner-up is another phrasing of the winning skill. That says nothing about
        // ambiguity, so it must not count against the margin.
        var decision = IntentRouter.Route([
            Phrase(SkillIds.DebtsHighestInterest, 0.9600),
            Phrase(SkillIds.DebtsHighestInterest, 0.9599),
            Phrase(SkillIds.PortfolioSummary, 0.8000),
        ]);

        decision.Accepted.Should().BeTrue();
        decision.Margin.Should().BeApproximately(0.16, 0.001);
    }

    [Fact]
    public void Accepts_when_every_candidate_belongs_to_one_skill()
    {
        var decision = IntentRouter.Route([Phrase(SkillIds.MarketFx, 0.95)]);

        decision.Accepted.Should().BeTrue();
        decision.SkillId.Should().Be(SkillIds.MarketFx);
    }

    [Fact]
    public void Rejects_an_empty_candidate_list_without_throwing()
    {
        var decision = IntentRouter.Route([]);

        decision.Accepted.Should().BeFalse();
        decision.Suggestions.Should().NotBeEmpty();
    }

    [Fact]
    public void Literal_evidence_rescues_a_thin_margin()
    {
        // "posisi NVDA saya gimana": strong score, margin under the normal gate, but the
        // question names a ticker the user actually holds.
        var candidates = new[]
        {
            Phrase(SkillIds.HoldingDetail, 0.9214),
            Phrase(SkillIds.PortfolioSummary, 0.9150),
        };

        IntentRouter.Route(candidates).Accepted.Should().BeFalse();
        IntentRouter.Route(candidates, [SkillIds.HoldingDetail]).Accepted.Should().BeTrue();
    }

    [Fact]
    public void Literal_evidence_for_a_different_skill_changes_nothing()
    {
        // "tolong belikan saya saham BBCA sekarang" names a real ticker, but it is a trade
        // instruction: the embedding ranks something else first, so it must still be refused.
        var candidates = new[]
        {
            Phrase(SkillIds.TransactionsRecent, 0.8821),
            Phrase(SkillIds.HoldingDetail, 0.8804),
        };

        IntentRouter.Route(candidates, [SkillIds.HoldingDetail]).Accepted.Should().BeFalse();
    }

    [Fact]
    public void Literal_evidence_cannot_rescue_a_question_the_embedding_rejects_outright()
    {
        // The relaxed gate is a step down, not a bypass.
        var decision = IntentRouter.Route(
            [Phrase(SkillIds.HoldingDetail, 0.72), Phrase(SkillIds.PortfolioSummary, 0.60)],
            [SkillIds.HoldingDetail]);

        decision.Accepted.Should().BeFalse();
    }

    [Fact]
    public void Suggestions_are_real_questions_from_the_catalogue()
    {
        var decision = IntentRouter.Route([
            Phrase(SkillIds.PortfolioSummary, 0.80),
            Phrase(SkillIds.DebtsDueSoon, 0.79),
        ]);

        var canonical = IntentCatalog.All.Select(i => i.CanonicalQuestion).ToList();
        decision.Suggestions.Should().OnlyContain(s => canonical.Contains(s));
        decision.Suggestions.Should().HaveCountLessThanOrEqualTo(ChatDefaults.SuggestionCount);
    }

    [Fact]
    public void Every_catalogue_phrase_maps_to_a_declared_skill()
    {
        // Guards against a phrase being added under a skill id that no skill implements,
        // which would route successfully and then fail to produce an answer.
        var declared = typeof(SkillIds).GetFields()
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet();

        IntentCatalog.All.Select(i => i.SkillId).Should().OnlyContain(id => declared.Contains(id));
        IntentCatalog.All.Should().OnlyContain(i => i.Phrases.Count > 0);
        IntentCatalog.All.Select(i => i.SkillId).Should().OnlyHaveUniqueItems();
    }
}
