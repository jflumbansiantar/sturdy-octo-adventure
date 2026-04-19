using MediatR;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Application.Ledger.Commands.CreateAccount;

public record CreateAccountCommand(
    string Id,
    string Code,
    string Name,
    AccountType Type,
    NormalBalanceType NormalBalance,
    decimal OpeningBalance = 0) : IRequest;
