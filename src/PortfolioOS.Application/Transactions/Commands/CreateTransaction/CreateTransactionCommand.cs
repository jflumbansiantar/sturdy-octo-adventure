using MediatR;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Application.Transactions.Commands.CreateTransaction;

public record CreateTransactionCommand(
    DateOnly Date,
    TransactionCategory Category,
    string Name,
    string Type,
    decimal Total,
    Market? Market = null,
    decimal? Shares = null,
    decimal? Price = null) : IRequest<Guid>;
