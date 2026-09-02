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
            return Refuse(decision with { SkillId = null }, facts);
        }

        var answer = await skill.ExecuteAsync(new ChatSkillContext(question, today, facts), ct);

        return answer with { SkillId = decision.SkillId, Confidence = decision.Score };
    }

    /// <summary>
    /// Finds skills the question supports through plain string matching rather than embeddings.
    /// </summary>
    /// <remarks>
    /// This is the "hybrid" half of retrieval. Cosine similarity is weak exactly where these
    /// signals are strong: a bare ticker carries almost no semantic content, and "april 2026"
    /// looks much like any other question about spending. Neither signal is allowed to answer
    /// on its own — it only lowers the bar for a skill the embedding already ranked first.
    /// </remarks>
    private async Task<IReadOnlyCollection<string>> FindLiteralSignalsAsync(
        string question, DateOnly today, CancellationToken ct)
    {
        var signals = new HashSet<string>(StringComparer.Ordinal);

        if (RelativePeriodParser.Parse(question, today) is not null)
        {
            signals.Add(SkillIds.TransactionsSpendInPeriod);
            signals.Add(SkillIds.TransactionsByCategory);
        }

        var words = question
            .Split([' ', ',', '.', '?', '!', ':', ';', '(', ')', '\'', '"'], StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToUpperInvariant())
            .ToHashSet(StringComparer.Ordinal);

        // Holding cards are written as "TICKER — Name. ...", so the ticker is the leading token.
        var holdings = await retriever.ListAsync(ChatDocumentKind.Holding, ct);
        if (holdings.Any(h => words.Contains(LeadingToken(h.Content))))
            signals.Add(SkillIds.HoldingDetail);

        return signals;
    }

    private static string LeadingToken(string content)
    {
        var end = content.IndexOf(' ');
        return (end < 0 ? content : content[..end]).ToUpperInvariant();
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

        return new ChatAnswer(
            text,
            Table: table,
            Confidence: decision.Score,
            Suggestions: decision.Suggestions);
    }
}
