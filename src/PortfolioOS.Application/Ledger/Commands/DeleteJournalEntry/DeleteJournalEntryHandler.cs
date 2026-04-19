using MediatR;
using Microsoft.EntityFrameworkCore;
using PortfolioOS.Application.Common.Interfaces;

namespace PortfolioOS.Application.Ledger.Commands.DeleteJournalEntry;

public class DeleteJournalEntryHandler : IRequestHandler<DeleteJournalEntryCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteJournalEntryHandler(IApplicationDbContext context) => _context = context;

    public async Task Handle(DeleteJournalEntryCommand cmd, CancellationToken ct)
    {
        var entry = await _context.JournalEntries.FirstOrDefaultAsync(e => e.Id == cmd.Id, ct)
            ?? throw new KeyNotFoundException($"Journal entry '{cmd.Id}' not found.");

        _context.JournalEntries.Remove(entry);
        await _context.SaveChangesAsync(ct);
    }
}
