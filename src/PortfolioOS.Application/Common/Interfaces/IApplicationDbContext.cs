using Microsoft.EntityFrameworkCore;
using PortfolioOS.Domain.Entities;

namespace PortfolioOS.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Holding> Holdings { get; }
    DbSet<Transaction> Transactions { get; }
    DbSet<PriceCache> PriceCaches { get; }
    DbSet<LedgerAccount> LedgerAccounts { get; }
    DbSet<JournalEntry> JournalEntries { get; }
    DbSet<JournalLine> JournalLines { get; }
    DbSet<Debt> Debts { get; }
    DbSet<AppSetting> AppSettings { get; }
    DbSet<ChatDocument> ChatDocuments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
