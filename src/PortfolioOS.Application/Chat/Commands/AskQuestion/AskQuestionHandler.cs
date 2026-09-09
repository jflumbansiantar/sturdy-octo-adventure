using MediatR;
using PortfolioOS.Application.Chat.Intents;
using PortfolioOS.Application.Chat.Skills;
using PortfolioOS.Application.Chat.Slots;
using PortfolioOS.Application.Common.Interfaces;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Application.Chat.Commands.AskQuestion;

/// <summary>
/// Routes a question to a skill and returns its answer.
/// </summary>
/// <remarks>
/// The division of labour is the whole design: retrieval decides *which* question is being
/// asked, the skill (via existing MediatR queries) produces every number, and a rejected
/// question yields suggestions rather than a guess. Nothing here can invent a figure, because
/// nothing here computes one.
/// </remarks>
public class AskQuestionHandler(
    IEmbeddingService embedder,
    IChatRetriever retriever,
    IEnumerable<IChatSkill> skills,
    TimeProvider timeProvider) : IRequestHandler<AskQuestionCommand, ChatAnswer>
{
    /// <summary>Enough phrases to see past a run of same-skill matches when computing the margin.</summary>
    private const int IntentCandidates = 15;
    private const int FactCandidates = 5;

    private readonly Dictionary<string, IChatSkill> _skills =
        skills.ToDictionary(s => s.SkillId, StringComparer.Ordinal);

    public async Task<ChatAnswer> Handle(AskQuestionCommand request, CancellationToken ct)
    {
        var question = request.Question.Trim();
        var vector = await embedder.EmbedAsync(question, EmbeddingKind.Query, ct);

        var intents = await retriever.SearchIntentsAsync(vector, IntentCandidates, ct);
        if (intents.Count == 0)
        {
            return new ChatAnswer(
                "Indeks pencarian masih kosong, jadi saya belum bisa memahami pertanyaan. " +
                "Jalankan POST /api/chat/reindex lebih dulu.");
        }

        var facts = await retriever.SearchFactsAsync(vector, FactCandidates, ct);

        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        var corroborated = await FindLiteralSignalsAsync(question, today, ct);

        var decision = IntentRouter.Route(intents, corroborated);

        if (!decision.Accepted)
            return Refuse(decision, facts);

        if (!_skills.TryGetValue(decision.SkillId!, out var skill))
        {
            // A phrase in the catalogue whose skill was never registered. Refusing is safer than
            // an unhandled exception, but it is a wiring bug rather than a user error.
            return Refuse(decision, facts);
        }

        var answer = await skill.ExecuteAsync(new ChatSkillContext(question, today, facts), ct);

        return answer with { SkillId = decision.SkillId, Confidence = decision.Score };
    }

    /// <summary>Gathers the non-embedding evidence this question carries.</summary>
    private async Task<IReadOnlyCollection<string>> FindLiteralSignalsAsync(
        string question, DateOnly today, CancellationToken ct)
    {
        var holdings = await retriever.ListAsync(ChatDocumentKind.Holding, ct);
        var tickers = holdings.Select(h => LiteralSignalDetector.TickerOf(h.Content));

        return LiteralSignalDetector.Detect(question, today, tickers);
    }

    /// <summary>
    /// The honest "I don't know". Still useful: it shows the nearest answerable questions and
    /// any records that matched the words, so a near miss is one click from being resolved.
    /// </summary>
    private static ChatAnswer Refuse(RoutingDecision decision, IReadOnlyList<ScoredDocument> facts)
    {
        var text = "Maaf, saya belum bisa menjawab pertanyaan itu dengan yakin dari data yang ada. " +
                   "Coba salah satu pertanyaan berikut, atau sebutkan lebih spesifik.";

        ChatTable? table = null;
        if (facts.Count > 0)
        {
            table = new ChatTable(
                ["Catatan yang mungkin relevan"],
                [.. facts.Take(3).Select(f => (IReadOnlyList<string>)[f.Content])]);
        }

        // An accepted decision carries no suggestions, so the wiring-bug path above would
        // otherwise leave the user staring at a refusal with nowhere to go.
        return new ChatAnswer(
            text,
            Table: table,
            Confidence: decision.Score,
            Suggestions: decision.Suggestions.Count > 0
                ? decision.Suggestions
                : IntentRouter.FallbackSuggestions);
    }
}
