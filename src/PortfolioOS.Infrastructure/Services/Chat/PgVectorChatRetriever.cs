using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PortfolioOS.Application.Chat;
using PortfolioOS.Domain.Enums;
using PortfolioOS.Infrastructure.Persistence;

namespace PortfolioOS.Infrastructure.Services.Chat;

/// <summary>
/// Nearest-neighbour search over <c>chat_documents</c> using pgvector's cosine operator.
/// </summary>
/// <remarks>
/// Hand-written SQL rather than LINQ: the entity stores the embedding as <c>float[]</c> so
/// PortfolioOS.Domain can stay dependency-free, which means there is no <c>Vector</c> property
/// for EF to translate <c>&lt;=&gt;</c> against. The query is small and fixed, so the trade is
/// cheap.
/// <para>
/// <c>&lt;=&gt;</c> is cosine *distance*; similarity is <c>1 - distance</c>. Vectors are stored
/// already L2-normalised, so this is a true cosine in [-1, 1].
/// </para>
/// </remarks>
public sealed class PgVectorChatRetriever(ApplicationDbContext db) : IChatRetriever
{
    public Task<IReadOnlyList<ScoredDocument>> SearchIntentsAsync(
        float[] queryVector, int take, CancellationToken ct = default) =>
        SearchAsync(queryVector, take, intentsOnly: true, ct);

    public Task<IReadOnlyList<ScoredDocument>> SearchFactsAsync(
        float[] queryVector, int take, CancellationToken ct = default) =>
        SearchAsync(queryVector, take, intentsOnly: false, ct);

    public async Task<IReadOnlyList<ScoredDocument>> ListAsync(
        ChatDocumentKind kind, CancellationToken ct = default)
    {
        // Score is meaningless here - these rows are for literal matching, not ranking.
        var rows = await db.ChatDocuments
            .AsNoTracking()
            .Where(d => d.Kind == kind)
            .Select(d => new { d.Kind, d.SkillId, d.SourceId, d.Content })
            .ToListAsync(ct);

        return [.. rows.Select(r => new ScoredDocument(r.Kind, r.SkillId, r.SourceId, r.Content, 0))];
    }

    private async Task<IReadOnlyList<ScoredDocument>> SearchAsync(
        float[] queryVector, int take, bool intentsOnly, CancellationToken ct)
    {
        var sql = $"""
            SELECT kind::text, skill_id, source_id, content,
                   1 - (embedding <=> $1::vector) AS score
            FROM chat_documents
            WHERE kind {(intentsOnly ? "=" : "<>")} 'IntentPhrase'
            ORDER BY embedding <=> $1::vector
            LIMIT $2
            """;

        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, connection);
        // Passed as text and cast in SQL, so this works without registering pgvector's type
        // handler on the raw ADO connection.
        cmd.Parameters.AddWithValue(ToVectorLiteral(queryVector));
        cmd.Parameters.AddWithValue(take);

        var results = new List<ScoredDocument>(take);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new ScoredDocument(
                Kind: Enum.Parse<ChatDocumentKind>(reader.GetString(0)),
                SkillId: reader.IsDBNull(1) ? null : reader.GetString(1),
                SourceId: reader.IsDBNull(2) ? null : reader.GetString(2),
                Content: reader.GetString(3),
                Score: reader.GetDouble(4)));
        }

        return results;
    }

    /// <summary>Formats a vector the way pgvector's text input expects: <c>[1,2,3]</c>.</summary>
    private static string ToVectorLiteral(float[] vector) =>
        string.Create(CultureInfo.InvariantCulture, $"[{string.Join(',', vector.Select(v => v.ToString("R", CultureInfo.InvariantCulture)))}]");
}
