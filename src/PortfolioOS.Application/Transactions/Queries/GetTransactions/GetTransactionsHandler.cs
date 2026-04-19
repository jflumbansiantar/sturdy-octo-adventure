using MediatR;
using Microsoft.EntityFrameworkCore;
using PortfolioOS.Application.Common.Interfaces;

namespace PortfolioOS.Application.Transactions.Queries.GetTransactions;

public class GetTransactionsHandler : IRequestHandler<GetTransactionsQuery, List<TransactionDto>>
{
    private readonly IApplicationDbContext _context;

    public GetTransactionsHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<TransactionDto>> Handle(GetTransactionsQuery request, CancellationToken ct)
    {
        var query = _context.Transactions.AsNoTracking();

        if (request.Category.HasValue)
            query = query.Where(t => t.Category == request.Category.Value);

        if (request.From.HasValue)
            query = query.Where(t => t.Date >= request.From.Value);

        if (request.To.HasValue)
            query = query.Where(t => t.Date <= request.To.Value);

        var transactions = await query.OrderByDescending(t => t.Date).ToListAsync(ct);

        return transactions.Select(t => new TransactionDto(
            t.Id, t.Date, t.Category.ToString(), t.Name, t.Type,
            t.Total, t.Market?.ToString(), t.Shares, t.Price, t.CreatedAt))
            .ToList();
    }
}
