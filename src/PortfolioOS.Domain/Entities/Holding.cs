using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Domain.Entities;

public class Holding
{
    public Guid Id { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public HoldingType Type { get; set; }
    public string SubType { get; set; } = string.Empty;
    public Market Market { get; set; }
    public decimal Shares { get; set; }
    public decimal AvgCost { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
