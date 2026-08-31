using MediatR;

namespace PortfolioOS.Application.MarketData.Queries.GetExchangeRate;

/// <param name="Rate">Units of <paramref name="Quote"/> per one <paramref name="Base"/>.</param>
/// <param name="IsLive">False when the provider was unreachable and a cached rate is in use.</param>
public record ExchangeRateDto(
    string Base,
    string Quote,
    decimal Rate,
    DateTimeOffset AsOf,
    bool IsLive);

/// <summary>
/// Lets a client convert between the two currencies the portfolio spans without having to
/// pull the whole portfolio summary just to read the rate off it.
/// </summary>
public record GetExchangeRateQuery : IRequest<ExchangeRateDto>;
