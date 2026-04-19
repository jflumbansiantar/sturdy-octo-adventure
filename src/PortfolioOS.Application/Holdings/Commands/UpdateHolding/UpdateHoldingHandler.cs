using MediatR;
using Microsoft.EntityFrameworkCore;
using PortfolioOS.Application.Common.Interfaces;

namespace PortfolioOS.Application.Holdings.Commands.UpdateHolding;

public class UpdateHoldingHandler : IRequestHandler<UpdateHoldingCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateHoldingHandler(IApplicationDbContext context) => _context = context;

    public async Task Handle(UpdateHoldingCommand cmd, CancellationToken ct)
    {
        var holding = await _context.Holdings.FirstOrDefaultAsync(h => h.Id == cmd.Id, ct)
            ?? throw new KeyNotFoundException($"Holding {cmd.Id} not found.");

        holding.Name      = cmd.Name;
        holding.Type      = cmd.Type;
        holding.SubType   = cmd.SubType;
        holding.Market    = cmd.Market;
        holding.Shares    = cmd.Shares;
        holding.AvgCost   = cmd.AvgCost;
        holding.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(ct);
    }
}
