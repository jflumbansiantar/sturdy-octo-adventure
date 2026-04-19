using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Domain.Entities;

public class Transaction
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public TransactionCategory Category { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public Market? Market { get; set; }
    public decimal? Shares { get; set; }
    public decimal? Price { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
