namespace PortfolioOS.Application.Chat;

/// <summary>
/// Retrieval constants shared by the EF mapping, the indexer and the router.
/// The threshold pair is the whole safety story of this feature, so it is documented rather
/// than buried as a magic number.
/// </summary>
public static class ChatDefaults
{
    /// <summary>Width of an <c>intfloat/multilingual-e5-small</c> embedding.</summary>
    public const int EmbeddingDimensions = 384;

    /// <summary>
    /// Minimum cosine score for the top-ranked intent phrase. This is the primary gate.
    /// </summary>
    /// <remarks>
    /// Deliberately high. E5 scores are compressed into a narrow band near the top — an entirely
    /// unrelated sentence ("resep nasi goreng pedas") still scored 0.84 against this catalogue —
    /// so a conventional 0.7 gate would accept literally everything.
    /// <para>
    /// Measured over the full 123-phrase catalogue: 17 held-out valid questions scored
    /// 0.8896-1.0000, six out-of-scope questions scored 0.8076-0.8821. 0.885 sits in that gap.
    /// The gap is real but narrow, so treat this as tuned-for-now rather than settled, and
    /// re-measure whenever phrases are added.
    /// </para>
    /// </remarks>
    public const double MinScore = 0.885;

    /// <summary>
    /// Minimum gap between the best phrase and the best phrase belonging to a *different* skill.
    /// Secondary gate, catching questions that score high against everything equally.
    /// </summary>
    /// <remarks>
    /// Kept deliberately loose. An earlier reading on a 12-phrase catalogue suggested margin was
    /// the stronger signal; on the full catalogue that reversed — margins of valid and invalid
    /// questions overlap outright ("halo" margins 0.034, above three genuine questions), so a
    /// strict margin gate rejected correct answers while letting nothing extra out.
    /// At 0.010 it only vetoes the genuinely flat cases (0.0015-0.0044) and defers to
    /// <see cref="MinScore"/> for everything else.
    /// </remarks>
    public const double MinMargin = 0.010;

    /// <summary>
    /// Relaxed gates used when a literal signal independently corroborates the chosen skill.
    /// </summary>
    /// <remarks>
    /// Short, entity-heavy questions ("posisi NVDA saya gimana", "pengeluaran april 2026 berapa")
    /// give the embedding little to work with and land just under the normal gates — 0.9214 with
    /// a thin margin, and 0.8765, respectively. But both name something concrete and verifiable:
    /// a ticker the user holds, a resolvable date. Agreement between two independent signals is
    /// better evidence than a high cosine alone, so the gates step down rather than off.
    /// <para>
    /// They step down, not away: a question with no embedding support at all still cannot pass,
    /// and evidence only counts for the skill that actually won.
    /// </para>
    /// </remarks>
    public const double CorroboratedMinScore = 0.86;
    public const double CorroboratedMinMargin = 0.003;

    /// <summary>How many suggestions to offer when a question is rejected.</summary>
    public const int SuggestionCount = 3;
}
