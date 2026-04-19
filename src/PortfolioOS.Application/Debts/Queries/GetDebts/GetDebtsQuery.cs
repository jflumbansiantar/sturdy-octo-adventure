using MediatR;

namespace PortfolioOS.Application.Debts.Queries.GetDebts;

public record GetDebtsQuery : IRequest<List<DebtDto>>;
