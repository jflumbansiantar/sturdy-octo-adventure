using FluentAssertions;
using PortfolioOS.Domain.Entities;
using PortfolioOS.Application.Common.Interfaces;
using Moq;
using Microsoft.EntityFrameworkCore;
using PortfolioOS.Application.Holdings.Commands.CreateHolding;
using PortfolioOS.Application.Holdings.Commands.DeleteHolding;
using PortfolioOS.Application.Holdings.Commands.UpdateHolding;
using PortfolioOS.Application.Holdings.Queries.GetHoldings;
using PortfolioOS.Domain.Enums;
using PortfolioOS.Infrastructure.Persistence;

namespace PortfolioOS.Application.Tests;

public class HoldingTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task CreateHolding_PersistsAndReturnsId()
    {
        await using var ctx = CreateContext();
        var handler = new CreateHoldingHandler(ctx);
        var cmd = new CreateHoldingCommand("AAPL", "Apple Inc.", HoldingType.Stock, "", Market.US, 10, 150m);

        var id = await handler.Handle(cmd, default);

        id.Should().NotBeEmpty();
        var holding = await ctx.Holdings.FindAsync(id);
        holding.Should().NotBeNull();
        holding!.Ticker.Should().Be("AAPL");
        holding.Shares.Should().Be(10);
    }

    [Fact]
    public async Task CreateHolding_NormalizesTickerToUpperCase()
    {
        await using var ctx = CreateContext();
        var handler = new CreateHoldingHandler(ctx);
        var cmd = new CreateHoldingCommand("aapl", "Apple Inc.", HoldingType.Stock, "", Market.US, 10, 150m);

        var id = await handler.Handle(cmd, default);

        var holding = await ctx.Holdings.FindAsync(id);
        holding!.Ticker.Should().Be("AAPL");
    }

    [Fact]
    public async Task UpdateHolding_ChangesFields()
    {
        await using var ctx = CreateContext();
        var createHandler = new CreateHoldingHandler(ctx);
        var id = await createHandler.Handle(
            new CreateHoldingCommand("MSFT", "Microsoft", HoldingType.Stock, "", Market.US, 5, 200m), default);

        var updateHandler = new UpdateHoldingHandler(ctx);
        await updateHandler.Handle(
            new UpdateHoldingCommand(id, "Microsoft Corp.", HoldingType.Stock, "", Market.US, 8, 210m), default);

        var holding = await ctx.Holdings.FindAsync(id);
        holding!.Name.Should().Be("Microsoft Corp.");
        holding.Shares.Should().Be(8);
        holding.AvgCost.Should().Be(210m);
    }

    [Fact]
    public async Task UpdateHolding_ThrowsWhenNotFound()
    {
        await using var ctx = CreateContext();
        var handler = new UpdateHoldingHandler(ctx);

        await handler.Invoking(h => h.Handle(
            new UpdateHoldingCommand(Guid.NewGuid(), "X", HoldingType.Stock, "", Market.US, 1, 1m), default))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task DeleteHolding_RemovesFromDb()
    {
        await using var ctx = CreateContext();
        var createHandler = new CreateHoldingHandler(ctx);
        var id = await createHandler.Handle(
            new CreateHoldingCommand("GOOG", "Alphabet", HoldingType.Stock, "", Market.US, 2, 100m), default);

        var deleteHandler = new DeleteHoldingHandler(ctx);
        await deleteHandler.Handle(new DeleteHoldingCommand(id), default);

        var holding = await ctx.Holdings.FindAsync(id);
        holding.Should().BeNull();
    }

    [Fact]
    public async Task DeleteHolding_ThrowsWhenNotFound()
    {
        await using var ctx = CreateContext();
        var handler = new DeleteHoldingHandler(ctx);

        await handler.Invoking(h => h.Handle(new DeleteHoldingCommand(Guid.NewGuid()), default))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetHoldings_ConvertsDollarValuesIntoBaseCurrency()
    {
        await using var ctx = CreateContext();
        ctx.Holdings.Add(new Holding
        {
            Id = Guid.NewGuid(), Ticker = "AAPL", Name = "Apple", Shares = 10, AvgCost = 100m,
            Type = HoldingType.Stock, Market = Market.US,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        });
        ctx.PriceCaches.Add(new PriceCache
        {
            Ticker = "AAPL", Currency = CurrencyType.USD,
            CurrentPrice = 200m, PreviousClose = 190m, UpdatedAt = DateTimeOffset.UtcNow
        });
        await ctx.SaveChangesAsync();

        var result = await new GetHoldingsHandler(ctx, FixedRate(16_000m))
            .Handle(new GetHoldingsQuery(), default);

        var dto = result.Single();

        // Per-share figures stay in the currency the instrument trades in...
        dto.CurrentPrice.Should().Be(200m);
        dto.PriceCurrency.Should().Be("USD");

        // ...while everything summable is expressed in rupiah, so it can be added to an
        // IDX position without producing a number that is neither currency.
        dto.MarketValue.Should().Be(10 * 200m * 16_000m);
        dto.CostBasis.Should().Be(10 * 100m * 16_000m);
        dto.GainLoss.Should().Be(16_000_000m);
        dto.DayGainLoss.Should().Be(10 * 10m * 16_000m);

        // Ratios are currency-free and must not be scaled by the rate.
        dto.GainLossPct.Should().Be(100m);
    }

    /// <summary>
    /// A deterministic USD-IDR rate. The handler converts dollar holdings into base currency,
    /// so a test rate keeps the expected values arithmetic rather than network-dependent.
    /// </summary>
    private static IExchangeRateService FixedRate(decimal usdIdr)
    {
        var stub = new Mock<IExchangeRateService>();
        stub.Setup(s => s.GetUsdIdrAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRate(usdIdr, DateTimeOffset.UtcNow, IsLive: true));
        return stub.Object;
    }

    [Fact]
    public async Task GetHoldings_ReturnsDtoWithZeroPriceWhenNoCacheEntry()
    {
        await using var ctx = CreateContext();
        var createHandler = new CreateHoldingHandler(ctx);
        await createHandler.Handle(
            new CreateHoldingCommand("TSLA", "Tesla", HoldingType.Stock, "", Market.US, 3, 250m), default);

        var getHandler = new GetHoldingsHandler(ctx, FixedRate(16_000m));
        var result = await getHandler.Handle(new GetHoldingsQuery(), default);

        result.Should().HaveCount(1);
        var dto = result[0];
        dto.Ticker.Should().Be("TSLA");
        dto.CurrentPrice.Should().Be(0m);
        dto.MarketValue.Should().Be(0m);
    }
}
