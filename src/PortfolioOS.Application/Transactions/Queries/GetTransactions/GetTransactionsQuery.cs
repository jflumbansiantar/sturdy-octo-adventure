using MediatR;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Application.Transactions.Queries.GetTransactions;

public record GetTransactionsQuery(
    TransactionCategory? Category = null,
    DateOnly? From = null,
    DateOnly? To = null) : IRequest<List<TransactionDto>>;
