using MediatR;
using Microsoft.EntityFrameworkCore;
using PortfolioOS.Application.Common.Interfaces;
using PortfolioOS.Domain.Entities;

namespace PortfolioOS.Application.Ledger.Queries.GetEntries;

public class GetEntriesHandler : IRequestHandler<GetEntriesQuery, List<JournalEntryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetEntriesHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<JournalEntryDto>> Handle(GetEntriesQuery request, CancellationToken ct)
    {
        IQueryable<JournalEntry> query = _context.JournalEntries
            .AsNoTracking()
            .Include(e => e.Lines)
                .ThenInclude(l => l.Account);

        if (request.From.HasValue)
            query = query.Where(e => e.Date >= request.From.Value);
        if (request.To.HasValue)
            query = query.Where(e => e.Date <= request.To.Value);

        var entries = await query.OrderByDescending(e => e.Date).ToListAsync(ct);

        return entries.Select(e => new JournalEntryDto(
            e.Id, e.Date, e.Description,
            e.Lines.Select(l => new JournalLineDto(
                l.Id, l.AccountId, l.Account?.Name ?? "", l.Debit, l.Credit))
                .ToList(),
            e.CreatedAt)).ToList();
    }
}
