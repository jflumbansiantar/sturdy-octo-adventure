using MediatR;
using Microsoft.EntityFrameworkCore;
using PortfolioOS.Application.Common.Interfaces;

namespace PortfolioOS.Application.Debts.Commands.UpdateDebt;

public class UpdateDebtHandler : IRequestHandler<UpdateDebtCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateDebtHandler(IApplicationDbContext context) => _context = context;

    public async Task Handle(UpdateDebtCommand cmd, CancellationToken ct)
    {
        var debt = await _context.Debts.FirstOrDefaultAsync(d => d.Id == cmd.Id, ct)
            ?? throw new KeyNotFoundException($"Debt {cmd.Id} not found.");

        debt.Name                = cmd.Name;
        debt.Type                = cmd.Type;
        debt.Balance             = cmd.Balance;
        debt.InterestRate        = cmd.InterestRate;
        debt.MonthlyInterestRate = cmd.MonthlyInterestRate;
        debt.Tenor               = cmd.Tenor;
        debt.MinimumPayment      = cmd.MinimumPayment;
        debt.DueDay              = cmd.DueDay;
        debt.Currency            = cmd.Currency;
        debt.DebtApp             = cmd.DebtApp;
        debt.Notes               = cmd.Notes;
        debt.Status              = cmd.Status;
        debt.UpdatedAt           = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(ct);
    }
}
