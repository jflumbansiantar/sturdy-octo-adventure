using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Domain.Entities;

public class LedgerAccount
{
    public string Id { get; set; } = string.Empty;   // e.g. 'A1000', 'L2000'
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AccountType Type { get; set; }
    public NormalBalanceType NormalBalance { get; set; }
    public decimal OpeningBalance { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<JournalLine> JournalLines { get; set; } = [];
}
