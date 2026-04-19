using MediatR;
using PortfolioOS.Application.Common.Interfaces;
using PortfolioOS.Domain.Entities;

namespace PortfolioOS.Application.Ledger.Commands.CreateAccount;

public class CreateAccountHandler : IRequestHandler<CreateAccountCommand>
{
    private readonly IApplicationDbContext _context;

    public CreateAccountHandler(IApplicationDbContext context) => _context = context;

    public async Task Handle(CreateAccountCommand cmd, CancellationToken ct)
    {
        var account = new LedgerAccount
        {
            Id             = cmd.Id.ToUpperInvariant(),
            Code           = cmd.Code,
            Name           = cmd.Name,
            Type           = cmd.Type,
            NormalBalance  = cmd.NormalBalance,
            OpeningBalance = cmd.OpeningBalance,
            CreatedAt      = DateTimeOffset.UtcNow,
            UpdatedAt      = DateTimeOffset.UtcNow
        };

        _context.LedgerAccounts.Add(account);
        await _context.SaveChangesAsync(ct);
    }
}
