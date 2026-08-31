using MediatR;
using PortfolioOS.Application.Common.Interfaces;

namespace PortfolioOS.Application.MarketData.Queries.GetExchangeRate;

public class GetExchangeRateHandler : IRequestHandler<GetExchangeRateQuery, ExchangeRateDto>
{
    private readonly IExchangeRateService _exchangeRate;

    public GetExchangeRateHandler(IExchangeRateService exchangeRate) => _exchangeRate = exchangeRate;

    public async Task<ExchangeRateDto> Handle(GetExchangeRateQuery request, CancellationToken ct)
    {
        var rate = await _exchangeRate.GetUsdIdrAsync(ct);
        return new ExchangeRateDto("USD", "IDR", rate.UsdIdr, rate.AsOf, rate.IsLive);
    }
}
