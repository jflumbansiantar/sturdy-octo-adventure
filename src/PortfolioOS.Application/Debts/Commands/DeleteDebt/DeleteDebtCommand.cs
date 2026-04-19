using MediatR;

namespace PortfolioOS.Application.Debts.Commands.DeleteDebt;

public record DeleteDebtCommand(Guid Id) : IRequest;
