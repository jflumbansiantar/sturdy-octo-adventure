using MediatR;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Application.Ledger.Commands.UpdateAccount;

public record UpdateAccountCommand(
    string Id,
    string Code,
    string Name,
    AccountType Type,
    NormalBalanceType NormalBalance,
    decimal OpeningBalance) : IRequest;
