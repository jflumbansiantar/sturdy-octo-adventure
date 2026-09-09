namespace PortfolioOS.Application.Common.Interfaces;

public sealed record ChatIndexResult(int Total, int Embedded, int Unchanged, int Removed);

/// <summary>
/// Keeps the chat corpus in step with the data. Safe to call repeatedly: rows whose text has
/// not changed are left alone rather than re-embedded.
/// </summary>
public interface IChatIndexService
{
    Task<ChatIndexResult> ReindexAsync(CancellationToken ct = default);
}
