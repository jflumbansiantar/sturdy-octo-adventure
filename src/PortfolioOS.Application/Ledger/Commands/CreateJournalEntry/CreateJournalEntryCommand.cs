using MediatR;

namespace PortfolioOS.Application.Ledger.Commands.CreateJournalEntry;

public record JournalLineInput(string AccountId, decimal Debit, decimal Credit);

public record CreateJournalEntryCommand(
    string Id,
    DateOnly Date,
    string Description,
    IReadOnlyList<JournalLineInput> Lines) : IRequest;
