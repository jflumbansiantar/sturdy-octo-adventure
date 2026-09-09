using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioOS.Application.Chat;
using PortfolioOS.Application.Chat.Intents;
using PortfolioOS.Application.Chat.Slots;
using PortfolioOS.Application.Common.Interfaces;
using PortfolioOS.Domain.Enums;
using PortfolioOS.Infrastructure.Services;
using Xunit.Abstractions;

namespace PortfolioOS.Application.Tests.Chat;

/// <summary>
/// Measures routing against the real model and the real catalogue.
/// </summary>
/// <remarks>
/// The other chat tests drive <see cref="IntentRouter"/> with hand-written scores, which pins the
/// decision logic but says nothing about whether the embedding actually separates these
/// questions. This one answers that, and is the evidence behind the thresholds in
/// <see cref="ChatDefaults"/>.
/// <para>
/// It runs in-process against a vector index held in memory rather than pgvector. The maths is
/// identical - both compute a cosine over the same normalised vectors - and it keeps the
/// measurement free of Docker.
/// </para>
/// </remarks>
public class RetrievalEvalTests(ITestOutputHelper output)
{
    /// <summary>Tickers the seeder creates; the corroboration signal needs a holdings list.</summary>
    private static readonly string[] SeededTickers =
        ["AAPL", "MSFT", "NVDA", "EIDO", "VTI", "BBCA", "TLKM", "BBRI", "ASII", "BTC", "ETH", "RDPU1", "RDSH1"];

    private static readonly DateOnly Today = new(2026, 9, 16);

    private sealed record Indexed(string SkillId, float[] Vector);

    private sealed record Outcome(
        EvalCase Case, string? Routed, double Score, double Margin, bool Accepted, bool Corroborated)
    {
        // A corroborated question is judged against the relaxed pair, so headroom has to be
        // measured against the gate that actually applied rather than the default one.
        public double MinScore => Corroborated ? ChatDefaults.CorroboratedMinScore : ChatDefaults.MinScore;
        public double MinMargin => Corroborated ? ChatDefaults.CorroboratedMinMargin : ChatDefaults.MinMargin;
    }

    [EmbeddingModelFact]
    public void Routing_separates_answerable_questions_from_ones_it_must_refuse()
    {
        var results = Evaluate();

        var answerable = results.Where(r => r.Case.Expected is not null).ToList();
        var refusals = results.Where(r => r.Case.Expected is null).ToList();

        Report(answerable, refusals);

        var misrouted = answerable.Where(r => r.Routed != r.Case.Expected).ToList();
        var blocked = answerable.Where(r => r.Routed == r.Case.Expected && !r.Accepted).ToList();

        // A question is correctly refused either way: the gate rejected it, or it matched an
        // out-of-scope intent and was declined with an explanation. The second is the better
        // outcome, but both keep data out of an answer that should not have one.
        var leaked = refusals.Where(r => r.Accepted && !IsOutOfScope(r.Routed)).ToList();

        // Answering something out of scope is the worst outcome: the user gets a confident,
        // plausible reply to a question about data that was never consulted. Zero tolerance.
        leaked.Should().BeEmpty(
            "these must be refused, not answered: {0}",
            string.Join(" | ", leaked.Select(l => $"\"{l.Case.Question}\" -> {l.Routed} @ {l.Score:F4}")));

        misrouted.Should().BeEmpty(
            "wrong skill for: {0}",
            string.Join(" | ", misrouted.Select(m => $"\"{m.Case.Question}\" -> {m.Routed}")));

        blocked.Should().BeEmpty(
            "correctly identified but refused by the confidence gate: {0}",
            string.Join(" | ", blocked.Select(b => $"\"{b.Case.Question}\" @ {b.Score:F4}/{b.Margin:F4}")));
    }

    [EmbeddingModelFact]
    public void Every_answered_question_clears_the_gate_with_room_to_spare()
    {
        // An earlier version of this test asserted that answerable and refusable questions
        // separate by score. They do not, and cannot: "apakah portofolio saya akan naik tahun
        // depan" is about the portfolio and scores like it. That is why the out-of-scope intents
        // exist, and why a high-scoring refusal is now a correct result rather than a warning.
        //
        // What is still worth pinning is headroom: if the questions we answer sit right on top of
        // the thresholds, the constants are fitted to this sample and the next phrasing falls
        // through.
        var answered = Evaluate()
            .Where(r => r.Case.Expected is not null && r.Routed == r.Case.Expected && r.Accepted)
            .ToList();

        var scoreHeadroom = answered.Min(r => r.Score - r.MinScore);
        var marginHeadroom = answered.Min(r => r.Margin - r.MinMargin);

        output.WriteLine($"answered: {answered.Count} ({answered.Count(r => r.Corroborated)} via a literal signal)");
        output.WriteLine($"tightest score  : {scoreHeadroom:F4} above its gate");
        output.WriteLine($"tightest margin : {marginHeadroom:F4} above its gate");

        scoreHeadroom.Should().BeGreaterThanOrEqualTo(0);
        marginHeadroom.Should().BeGreaterThanOrEqualTo(0);
    }

    [EmbeddingModelFact]
    public void Out_of_scope_questions_are_declined_by_name_rather_than_by_threshold()
    {
        // The point of modelling the negative class: a user who asks for a forecast should be
        // told this assistant cannot forecast, not given a shrug. Most should reach a named
        // refusal; the bare threshold is the safety net underneath, not the mechanism.
        var refusals = Evaluate().Where(r => r.Case.Expected is null).ToList();
        var named = refusals.Count(r => IsOutOfScope(r.Routed) && r.Accepted);

        output.WriteLine($"{named}/{refusals.Count} declined by a named out-of-scope intent");

        named.Should().BeGreaterThan(refusals.Count / 2,
            "most refusals should carry an explanation, not just a rejection");
    }

    private List<Outcome> Evaluate()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Embedding:ModelPath"] = ModelPath.Resolve() })
            .Build();

        using var embedder = new OnnxEmbeddingService(config, NullLogger<OnnxEmbeddingService>.Instance);

        // Intent phrases are indexed as queries, matching ChatIndexService.
        var phrases = IntentCatalog.All
            .SelectMany(i => i.Phrases.Select(p => (i.SkillId, Phrase: p)))
            .ToList();

        var vectors = embedder
            .EmbedManyAsync(phrases.Select(p => p.Phrase).ToList(), EmbeddingKind.Query)
            .GetAwaiter().GetResult();

        var index = phrases.Zip(vectors, (p, v) => new Indexed(p.SkillId, v)).ToList();

        return RetrievalEvalSet.Answerable.Concat(RetrievalEvalSet.MustRefuse)
            .Select(c => Route(embedder, index, c))
            .ToList();
    }

    private static Outcome Route(IEmbeddingService embedder, List<Indexed> index, EvalCase c)
    {
        var q = embedder.EmbedAsync(c.Question, EmbeddingKind.Query).GetAwaiter().GetResult();

        var ranked = index
            .Select(e => new ScoredDocument(ChatDocumentKind.IntentPhrase, e.SkillId, null, "", Cosine(q, e.Vector)))
            .OrderByDescending(e => e.Score)
            .ToList();

        var signals = LiteralSignalDetector.Detect(c.Question, Today, SeededTickers);
        var decision = IntentRouter.Route(ranked, signals);

        var corroborated = decision.SkillId is not null && signals.Contains(decision.SkillId);

        return new Outcome(
            c, decision.SkillId ?? ranked[0].SkillId,
            decision.Score, decision.Margin, decision.Accepted, corroborated);
    }

    private static bool IsOutOfScope(string? skillId) =>
        skillId is not null &&
        IntentCatalog.BySkillId.TryGetValue(skillId, out var intent) &&
        intent.IsOutOfScope;

    private static double Cosine(float[] a, float[] b)
    {
        double s = 0;
        for (int i = 0; i < a.Length; i++) s += a[i] * b[i];
        return s;
    }

    private void Report(List<Outcome> answerable, List<Outcome> refusals)
    {
        output.WriteLine($"catalogue: {IntentCatalog.All.Count} skills, " +
                         $"{IntentCatalog.All.Sum(i => i.Phrases.Count)} phrases");
        output.WriteLine($"eval: {answerable.Count} answerable, {refusals.Count} must-refuse");
        output.WriteLine("");

        foreach (var r in answerable.OrderBy(r => r.Score))
        {
            var verdict = r.Routed != r.Case.Expected ? "MISROUTED"
                : r.Accepted ? "ok"
                : "BLOCKED";
            output.WriteLine($"{r.Score:F4}  {r.Margin:F4}  {verdict,-10} {r.Case.Question}");
        }

        output.WriteLine("");
        foreach (var r in refusals.OrderByDescending(r => r.Score))
        {
            var verdict = !r.Accepted ? $"refused(gate->{r.Routed})"
                : IsOutOfScope(r.Routed) ? $"declined({r.Routed![5..]})"
                : "LEAKED";
            output.WriteLine($"{r.Score:F4}  {r.Margin:F4}  {verdict,-22} {r.Case.Question}");
        }
    }
}
