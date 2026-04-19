namespace PortfolioOS.Domain.Entities;

public class JournalEntry
{
    public string Id { get; set; } = string.Empty;   // e.g. 'JE001', 'JE002'
    public DateOnly Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<JournalLine> Lines { get; set; } = [];
}
