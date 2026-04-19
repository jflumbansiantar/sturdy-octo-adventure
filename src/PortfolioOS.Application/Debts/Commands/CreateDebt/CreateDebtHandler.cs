using MediatR;
using PortfolioOS.Application.Common.Interfaces;
using PortfolioOS.Domain.Entities;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Application.Debts.Commands.CreateDebt;

public class CreateDebtHandler : IRequestHandler<CreateDebtCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateDebtHandler(IApplicationDbContext context) => _context = context;

    public async Task<Guid> Handle(CreateDebtCommand cmd, CancellationToken ct)
    {
        var debt = new Debt
        {
            Id                  = Guid.NewGuid(),
            Name                = cmd.Name,
            Type                = cmd.Type,
            Balance             = cmd.Balance,
            InterestRate        = cmd.InterestRate,
            MonthlyInterestRate = cmd.MonthlyInterestRate,
            Tenor               = cmd.Tenor,
            MinimumPayment      = cmd.MinimumPayment,
            DueDay              = cmd.DueDay,
            Currency            = cmd.Currency,
            DebtApp             = cmd.DebtApp,
            Notes               = cmd.Notes,
            Status              = DebtStatus.Active,
            CreatedAt           = DateTimeOffset.UtcNow,
            UpdatedAt           = DateTimeOffset.UtcNow
        };

        _context.Debts.Add(debt);
        await _context.SaveChangesAsync(ct);
        return debt.Id;
    }
}
