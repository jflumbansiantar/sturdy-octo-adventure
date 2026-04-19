using MediatR;
using Microsoft.EntityFrameworkCore;
using PortfolioOS.Application.Common.Interfaces;

namespace PortfolioOS.Application.Transactions.Commands.DeleteTransaction;

public class DeleteTransactionHandler : IRequestHandler<DeleteTransactionCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteTransactionHandler(IApplicationDbContext context) => _context = context;

    public async Task Handle(DeleteTransactionCommand cmd, CancellationToken ct)
    {
        var tx = await _context.Transactions.FirstOrDefaultAsync(t => t.Id == cmd.Id, ct)
            ?? throw new KeyNotFoundException($"Transaction {cmd.Id} not found.");

        _context.Transactions.Remove(tx);
        await _context.SaveChangesAsync(ct);
    }
}
