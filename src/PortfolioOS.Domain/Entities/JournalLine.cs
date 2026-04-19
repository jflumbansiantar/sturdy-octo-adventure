namespace PortfolioOS.Domain.Entities;

public class JournalLine
{
    public Guid Id { get; set; }
    public string EntryId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }

    public JournalEntry Entry { get; set; } = null!;
    public LedgerAccount Account { get; set; } = null!;
}
