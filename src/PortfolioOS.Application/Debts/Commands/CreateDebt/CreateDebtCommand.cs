using MediatR;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Application.Debts.Commands.CreateDebt;

public record CreateDebtCommand(
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
    string Notes) : IRequest<Guid>;
