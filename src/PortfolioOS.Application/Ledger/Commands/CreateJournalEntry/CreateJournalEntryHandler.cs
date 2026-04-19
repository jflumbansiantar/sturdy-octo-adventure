using MediatR;
using PortfolioOS.Application.Common.Interfaces;
using PortfolioOS.Domain.Entities;

namespace PortfolioOS.Application.Ledger.Commands.CreateJournalEntry;

public class CreateJournalEntryHandler : IRequestHandler<CreateJournalEntryCommand>
{
    private readonly IApplicationDbContext _context;

    public CreateJournalEntryHandler(IApplicationDbContext context) => _context = context;

    public async Task Handle(CreateJournalEntryCommand cmd, CancellationToken ct)
    {
        var totalDebits  = cmd.Lines.Sum(l => l.Debit);
        var totalCredits = cmd.Lines.Sum(l => l.Credit);

        if (totalDebits != totalCredits)
            throw new InvalidOperationException(
                $"Journal entry must balance: debits ({totalDebits}) ≠ credits ({totalCredits}).");

        var entry = new JournalEntry
        {
            Id          = cmd.Id.ToUpperInvariant(),
            Date        = cmd.Date,
            Description = cmd.Description,
            CreatedAt   = DateTimeOffset.UtcNow,
            UpdatedAt   = DateTimeOffset.UtcNow,
            Lines       = cmd.Lines.Select(l => new JournalLine
            {
                Id        = Guid.NewGuid(),
                AccountId = l.AccountId,
                Debit     = l.Debit,
                Credit    = l.Credit
            }).ToList()
        };

        _context.JournalEntries.Add(entry);
        await _context.SaveChangesAsync(ct);
    }
}
