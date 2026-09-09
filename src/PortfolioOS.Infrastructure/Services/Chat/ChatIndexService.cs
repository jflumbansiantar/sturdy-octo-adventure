using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PortfolioOS.Application.Chat;
using PortfolioOS.Application.Chat.Intents;
using PortfolioOS.Application.Common.Interfaces;
using PortfolioOS.Domain.Entities;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Infrastructure.Services.Chat;

/// <summary>
/// Rebuilds <c>chat_documents</c> from the intent catalogue and the live data.
/// </summary>
/// <remarks>
/// A full rebuild rather than incremental bookkeeping: the corpus is a couple of hundred rows,
/// and content hashing means an unchanged rebuild embeds nothing at all, so the simple approach
/// costs a query and no model time. That trade stops holding somewhere past ~10k rows.
/// </remarks>
public sealed class ChatIndexService(
    IApplicationDbContext db,
    IEmbeddingService embedder,
    ILogger<ChatIndexService> logger) : IChatIndexService
{
    /// <summary>Text destined for the index, before it is known whether it needs embedding.</summary>
    private sealed record Desired(ChatDocumentKind Kind, string SourceId, string? SkillId, string Content)
    {
        public string Hash { get; } = Sha256(Content);
    }

    public async Task<ChatIndexResult> ReindexAsync(CancellationToken ct = default)
    {
        var desired = await BuildDesiredAsync(ct);
        var existing = await db.ChatDocuments.ToListAsync(ct);

        var byKey = existing.ToDictionary(d => (d.Kind, d.SourceId ?? ""), d => d);
        var desiredKeys = desired.Select(d => (d.Kind, d.SourceId)).ToHashSet();

        // Rows whose source record is gone, or whose kind was retired.
        var stale = existing.Where(e => !desiredKeys.Contains((e.Kind, e.SourceId ?? ""))).ToList();
        if (stale.Count > 0) db.ChatDocuments.RemoveRange(stale);

        var toEmbed = new List<Desired>();
        foreach (var d in desired)
        {
            if (byKey.TryGetValue((d.Kind, d.SourceId), out var row) && row.ContentHash == d.Hash)
                continue;   // text identical - the stored vector is still correct
            toEmbed.Add(d);
        }

        if (toEmbed.Count > 0)
        {
            // Intent phrases are compared against questions, so they are embedded as queries
            // too; fact cards are genuine documents. Mixing these up costs real accuracy.
            foreach (var group in toEmbed.GroupBy(d => d.Kind == ChatDocumentKind.IntentPhrase
                         ? EmbeddingKind.Query
                         : EmbeddingKind.Passage))
            {
                var batch = group.ToList();
                var vectors = await embedder.EmbedManyAsync(
                    batch.Select(b => b.Content).ToList(), group.Key, ct);

                for (int i = 0; i < batch.Count; i++)
                {
                    var d = batch[i];
                    if (byKey.TryGetValue((d.Kind, d.SourceId), out var row))
                    {
                        row.Content = d.Content;
                        row.ContentHash = d.Hash;
                        row.SkillId = d.SkillId;
                        row.Embedding = vectors[i];
                        row.UpdatedAt = DateTimeOffset.UtcNow;
                    }
                    else
                    {
                        db.ChatDocuments.Add(new ChatDocument
                        {
                            Id = Guid.NewGuid(),
                            Kind = d.Kind,
                            SourceId = d.SourceId,
                            SkillId = d.SkillId,
                            Content = d.Content,
                            ContentHash = d.Hash,
                            Embedding = vectors[i],
                            UpdatedAt = DateTimeOffset.UtcNow,
                        });
                    }
                }
            }
        }

        await db.SaveChangesAsync(ct);

        var result = new ChatIndexResult(
            Total: desired.Count,
            Embedded: toEmbed.Count,
            Unchanged: desired.Count - toEmbed.Count,
            Removed: stale.Count);

        logger.LogInformation(
            "Chat index rebuilt: {Total} documents ({Embedded} embedded, {Unchanged} unchanged, {Removed} removed)",
            result.Total, result.Embedded, result.Unchanged, result.Removed);

        return result;
    }

    private async Task<List<Desired>> BuildDesiredAsync(CancellationToken ct)
    {
        var desired = new List<Desired>();

        foreach (var intent in IntentCatalog.All)
        {
            for (int i = 0; i < intent.Phrases.Count; i++)
            {
                desired.Add(new Desired(
                    ChatDocumentKind.IntentPhrase,
                    // Index-based so a reworded phrase updates in place instead of orphaning a row.
                    $"{intent.SkillId}#{i}",
                    intent.SkillId,
                    intent.Phrases[i]));
            }
        }

        foreach (var h in await db.Holdings.AsNoTracking().ToListAsync(ct))
            desired.Add(new Desired(ChatDocumentKind.Holding, h.Id.ToString(), null, FactCardBuilder.ForHolding(h)));

        foreach (var d in await db.Debts.AsNoTracking().ToListAsync(ct))
            desired.Add(new Desired(ChatDocumentKind.Debt, d.Id.ToString(), null, FactCardBuilder.ForDebt(d)));

        foreach (var t in await db.Transactions.AsNoTracking().ToListAsync(ct))
            desired.Add(new Desired(ChatDocumentKind.Transaction, t.Id.ToString(), null, FactCardBuilder.ForTransaction(t)));

        foreach (var e in await db.JournalEntries.AsNoTracking().ToListAsync(ct))
            desired.Add(new Desired(ChatDocumentKind.JournalEntry, e.Id, null, FactCardBuilder.ForJournalEntry(e)));

        foreach (var a in await db.LedgerAccounts.AsNoTracking().ToListAsync(ct))
            desired.Add(new Desired(ChatDocumentKind.LedgerAccount, a.Id, null, FactCardBuilder.ForLedgerAccount(a)));

        return desired;
    }

    private static string Sha256(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
