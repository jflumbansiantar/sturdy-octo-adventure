using FluentAssertions;
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
    public async Task GetHoldings_ReturnsDtoWithZeroPriceWhenNoCacheEntry()
    {
        await using var ctx = CreateContext();
        var createHandler = new CreateHoldingHandler(ctx);
        await createHandler.Handle(
            new CreateHoldingCommand("TSLA", "Tesla", HoldingType.Stock, "", Market.US, 3, 250m), default);

        var getHandler = new GetHoldingsHandler(ctx);
        var result = await getHandler.Handle(new GetHoldingsQuery(), default);

        result.Should().HaveCount(1);
        var dto = result[0];
        dto.Ticker.Should().Be("TSLA");
        dto.CurrentPrice.Should().Be(0m);
        dto.MarketValue.Should().Be(0m);
    }
}
