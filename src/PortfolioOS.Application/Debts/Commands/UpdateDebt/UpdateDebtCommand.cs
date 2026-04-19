using MediatR;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Application.Debts.Commands.UpdateDebt;

public record UpdateDebtCommand(
    Guid Id,
    string Name,
    DebtType Type,
    decimal Balance,
    decimal InterestRate,
    decimal? MonthlyInterestRate,
    int? Tenor,
    decimal MinimumPayment,
    int DueDay,
    CurrencyType Currency,
    string DebtApp,
    string Notes,
    DebtStatus Status) : IRequest;
