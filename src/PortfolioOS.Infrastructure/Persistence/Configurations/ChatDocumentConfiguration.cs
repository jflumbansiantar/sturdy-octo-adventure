using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pgvector;
using PortfolioOS.Application.Chat;
using PortfolioOS.Domain.Entities;

namespace PortfolioOS.Infrastructure.Persistence.Configurations;

/// <param name="isNpgsql">
/// False when running on the in-memory provider used by the unit tests, which has no
/// <c>vector</c> type. The embedding is then left unmapped - nothing in those tests reads it,
/// and mapping it would prevent the model from building at all.
/// </param>
public class ChatDocumentConfiguration(bool isNpgsql = true) : IEntityTypeConfiguration<ChatDocument>
{
    public void Configure(EntityTypeBuilder<ChatDocument> builder)
    {
        builder.ToTable("chat_documents");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasColumnType("chat_document_kind")
            .IsRequired();

        builder.Property(x => x.SourceId).HasColumnName("source_id").HasMaxLength(100);
        builder.Property(x => x.SkillId).HasColumnName("skill_id").HasMaxLength(100);
        builder.Property(x => x.Content).HasColumnName("content").IsRequired();
        builder.Property(x => x.ContentHash).HasColumnName("content_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // The entity exposes float[] so PortfolioOS.Domain keeps its zero-dependency rule;
        // the Pgvector type only exists here, at the persistence boundary.
        if (isNpgsql)
        {
            builder.Property(x => x.Embedding)
                .HasColumnName("embedding")
                .HasColumnType($"vector({ChatDefaults.EmbeddingDimensions})")
                .HasConversion(
                    v => new Vector(v),
                    v => v.ToArray(),
                    new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<float[]>(
                        (a, b) => a!.SequenceEqual(b!),
                        v => v.Aggregate(0, (acc, f) => HashCode.Combine(acc, f.GetHashCode())),
                        v => v.ToArray()))
                .IsRequired();
        }
        else
        {
            builder.Ignore(x => x.Embedding);
        }

        // Routing only ever searches intent phrases; free-text search only ever searches the rest.
        builder.HasIndex(x => x.Kind).HasDatabaseName("idx_chat_documents_kind");

        // One card per source record, so a reindex can upsert rather than duplicate.
        builder.HasIndex(x => new { x.Kind, x.SourceId }).HasDatabaseName("idx_chat_documents_source");

        // No ANN index on purpose: at this corpus size (~150 rows) a sequential scan over
        // 384-dim vectors is sub-millisecond and beats HNSW, which also costs recall.
        // Revisit past ~10k rows.
    }
}
