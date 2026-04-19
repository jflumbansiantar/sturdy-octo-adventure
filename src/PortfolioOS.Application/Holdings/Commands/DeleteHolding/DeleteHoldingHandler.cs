using MediatR;
using Microsoft.EntityFrameworkCore;
using PortfolioOS.Application.Common.Interfaces;

namespace PortfolioOS.Application.Holdings.Commands.DeleteHolding;

public class DeleteHoldingHandler : IRequestHandler<DeleteHoldingCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteHoldingHandler(IApplicationDbContext context) => _context = context;

    public async Task Handle(DeleteHoldingCommand cmd, CancellationToken ct)
    {
        var holding = await _context.Holdings.FirstOrDefaultAsync(h => h.Id == cmd.Id, ct)
            ?? throw new KeyNotFoundException($"Holding {cmd.Id} not found.");

        _context.Holdings.Remove(holding);
        await _context.SaveChangesAsync(ct);
    }
}
