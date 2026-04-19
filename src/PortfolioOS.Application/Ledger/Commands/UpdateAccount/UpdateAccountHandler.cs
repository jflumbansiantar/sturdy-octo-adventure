using MediatR;
using Microsoft.EntityFrameworkCore;
using PortfolioOS.Application.Common.Interfaces;

namespace PortfolioOS.Application.Ledger.Commands.UpdateAccount;

public class UpdateAccountHandler : IRequestHandler<UpdateAccountCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateAccountHandler(IApplicationDbContext context) => _context = context;

    public async Task Handle(UpdateAccountCommand cmd, CancellationToken ct)
    {
        var account = await _context.LedgerAccounts.FirstOrDefaultAsync(a => a.Id == cmd.Id, ct)
            ?? throw new KeyNotFoundException($"Ledger account '{cmd.Id}' not found.");

        account.Code           = cmd.Code;
        account.Name           = cmd.Name;
        account.Type           = cmd.Type;
        account.NormalBalance  = cmd.NormalBalance;
        account.OpeningBalance = cmd.OpeningBalance;
        account.UpdatedAt      = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(ct);
    }
}
