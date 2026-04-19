using MediatR;
using PortfolioOS.Application.Common.Interfaces;
using PortfolioOS.Domain.Entities;

namespace PortfolioOS.Application.Holdings.Commands.CreateHolding;

public class CreateHoldingHandler : IRequestHandler<CreateHoldingCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateHoldingHandler(IApplicationDbContext context) => _context = context;

    public async Task<Guid> Handle(CreateHoldingCommand cmd, CancellationToken ct)
    {
        var holding = new Holding
        {
            Id        = Guid.NewGuid(),
            Ticker    = cmd.Ticker.ToUpperInvariant(),
            Name      = cmd.Name,
            Type      = cmd.Type,
            SubType   = cmd.SubType,
            Market    = cmd.Market,
            Shares    = cmd.Shares,
            AvgCost   = cmd.AvgCost,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.Holdings.Add(holding);
        await _context.SaveChangesAsync(ct);
        return holding.Id;
    }
}
