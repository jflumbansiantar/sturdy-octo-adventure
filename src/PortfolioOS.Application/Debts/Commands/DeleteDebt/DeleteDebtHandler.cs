using MediatR;
using Microsoft.EntityFrameworkCore;
using PortfolioOS.Application.Common.Interfaces;

namespace PortfolioOS.Application.Debts.Commands.DeleteDebt;

public class DeleteDebtHandler : IRequestHandler<DeleteDebtCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteDebtHandler(IApplicationDbContext context) => _context = context;

    public async Task Handle(DeleteDebtCommand cmd, CancellationToken ct)
    {
        var debt = await _context.Debts.FirstOrDefaultAsync(d => d.Id == cmd.Id, ct)
            ?? throw new KeyNotFoundException($"Debt {cmd.Id} not found.");

        _context.Debts.Remove(debt);
        await _context.SaveChangesAsync(ct);
    }
}
