namespace PortfolioOS.Web.Models;

public record ExchangeRateModel(
    string Base,
    string Quote,
    decimal Rate,
    DateTimeOffset AsOf,
    bool IsLive);
