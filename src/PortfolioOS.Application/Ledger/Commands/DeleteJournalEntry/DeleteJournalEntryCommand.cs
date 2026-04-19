using MediatR;

namespace PortfolioOS.Application.Ledger.Commands.DeleteJournalEntry;

public record DeleteJournalEntryCommand(string Id) : IRequest;
