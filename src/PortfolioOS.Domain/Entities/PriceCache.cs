using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Domain.Entities;

public class PriceCache
{
    public string Ticker { get; set; } = string.Empty;
    public CurrencyType Currency { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal PreviousClose { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
