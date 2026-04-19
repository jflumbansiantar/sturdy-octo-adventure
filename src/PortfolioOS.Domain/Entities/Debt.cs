using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Domain.Entities;

public class Debt
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DebtType Type { get; set; }
    public decimal Balance { get; set; }
    public decimal InterestRate { get; set; }
    public decimal? MonthlyInterestRate { get; set; }
    public int? Tenor { get; set; }
    public decimal MinimumPayment { get; set; }
    public int DueDay { get; set; }
    public CurrencyType Currency { get; set; }
    public string DebtApp { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DebtStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
