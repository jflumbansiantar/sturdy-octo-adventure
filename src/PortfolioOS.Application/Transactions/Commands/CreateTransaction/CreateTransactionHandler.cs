using MediatR;
using Microsoft.EntityFrameworkCore;
using PortfolioOS.Application.Common.Interfaces;
using PortfolioOS.Domain.Entities;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Application.Transactions.Commands.CreateTransaction;

public class CreateTransactionHandler : IRequestHandler<CreateTransactionCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateTransactionHandler(IApplicationDbContext context) => _context = context;

    public async Task<Guid> Handle(CreateTransactionCommand cmd, CancellationToken ct)
    {
        var transaction = new Transaction
        {
            Id        = Guid.NewGuid(),
            Date      = cmd.Date,
            Category  = cmd.Category,
            Name      = cmd.Name,
            Type      = cmd.Type,
            Total     = cmd.Total,
            Market    = cmd.Market,
            Shares    = cmd.Shares,
            Price     = cmd.Price,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.Transactions.Add(transaction);

        // For STOCK transactions, update the holding's shares and avg cost
        if (cmd.Category == TransactionCategory.Stock && cmd.Shares.HasValue)
        {
            var holding = await _context.Holdings
                .FirstOrDefaultAsync(h => h.Ticker == cmd.Name.ToUpperInvariant(), ct);

            if (holding != null)
            {
                if (cmd.Type.Equals("BUY", StringComparison.OrdinalIgnoreCase))
                {
                    var newShares = holding.Shares + cmd.Shares.Value;
                    // Weighted average cost
                    holding.AvgCost   = (holding.Shares * holding.AvgCost + cmd.Total) / newShares;
                    holding.Shares    = newShares;
                    holding.UpdatedAt = DateTimeOffset.UtcNow;
                }
                else if (cmd.Type.Equals("SELL", StringComparison.OrdinalIgnoreCase))
                {
                    holding.Shares    = Math.Max(0, holding.Shares - cmd.Shares.Value);
                    holding.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }
        }

        await _context.SaveChangesAsync(ct);
        return transaction.Id;
    }
}
