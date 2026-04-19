using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PortfolioOS.Application.Transactions.Commands.CreateTransaction;
using PortfolioOS.Application.Transactions.Commands.DeleteTransaction;
using PortfolioOS.Application.Transactions.Queries.GetTransactions;
using PortfolioOS.Domain.Entities;
using PortfolioOS.Domain.Enums;
using PortfolioOS.Infrastructure.Persistence;

namespace PortfolioOS.Application.Tests;

public class TransactionTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task CreateTransaction_PersistsAndReturnsId()
    {
        await using var ctx = CreateContext();
        var handler = new CreateTransactionHandler(ctx);
        var cmd = new CreateTransactionCommand(
            DateOnly.FromDateTime(DateTime.Today), TransactionCategory.Income,
            "Salary", "Credit", 5000m);

        var id = await handler.Handle(cmd, default);

        id.Should().NotBeEmpty();
        var tx = await ctx.Transactions.FindAsync(id);
        tx.Should().NotBeNull();
        tx!.Total.Should().Be(5000m);
        tx.Category.Should().Be(TransactionCategory.Income);
    }

    [Fact]
    public async Task CreateStockBuy_UpdatesHoldingAvgCostAndShares()
    {
        await using var ctx = CreateContext();
        ctx.Holdings.Add(new Holding
        {
            Id = Guid.NewGuid(), Ticker = "AAPL", Name = "Apple", Shares = 10, AvgCost = 100m,
            Type = HoldingType.Stock, Market = Market.US,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        });
        await ctx.SaveChangesAsync();

        var handler = new CreateTransactionHandler(ctx);
        // Buy 10 more shares at $120 each = $1200 total
        var cmd = new CreateTransactionCommand(
            DateOnly.FromDateTime(DateTime.Today), TransactionCategory.Stock,
            "AAPL", "BUY", 1200m, Market.US, 10m, 120m);

        await handler.Handle(cmd, default);

        var holding = await ctx.Holdings.FirstAsync(h => h.Ticker == "AAPL");
        holding.Shares.Should().Be(20m);
        holding.AvgCost.Should().Be(110m); // (10*100 + 10*120) / 20 = 110
    }

    [Fact]
    public async Task GetTransactions_FiltersByCategory()
    {
        await using var ctx = CreateContext();
        var handler = new CreateTransactionHandler(ctx);
        await handler.Handle(new CreateTransactionCommand(
            DateOnly.FromDateTime(DateTime.Today), TransactionCategory.Income, "Salary", "Credit", 5000m), default);
        await handler.Handle(new CreateTransactionCommand(
            DateOnly.FromDateTime(DateTime.Today), TransactionCategory.Expense, "Rent", "Debit", 1500m), default);

        var getHandler = new GetTransactionsHandler(ctx);
        var income = await getHandler.Handle(
            new GetTransactionsQuery(TransactionCategory.Income, null, null), default);

        income.Should().HaveCount(1);
        income[0].Name.Should().Be("Salary");
    }

    [Fact]
    public async Task DeleteTransaction_RemovesFromDb()
    {
        await using var ctx = CreateContext();
        var createHandler = new CreateTransactionHandler(ctx);
        var id = await createHandler.Handle(new CreateTransactionCommand(
            DateOnly.FromDateTime(DateTime.Today), TransactionCategory.Expense, "Coffee", "Debit", 5m), default);

        var deleteHandler = new DeleteTransactionHandler(ctx);
        await deleteHandler.Handle(new DeleteTransactionCommand(id), default);

        var tx = await ctx.Transactions.FindAsync(id);
        tx.Should().BeNull();
    }
}
