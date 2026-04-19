using MediatR;

namespace PortfolioOS.Application.Ledger.Queries.GetEntries;

public record GetEntriesQuery(DateOnly? From = null, DateOnly? To = null) : IRequest<List<JournalEntryDto>>;
