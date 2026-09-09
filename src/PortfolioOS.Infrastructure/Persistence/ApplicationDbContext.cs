using Microsoft.EntityFrameworkCore;
using PortfolioOS.Application.Common.Interfaces;
using PortfolioOS.Domain.Entities;
using PortfolioOS.Infrastructure.Persistence.Configurations;

namespace PortfolioOS.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Holding> Holdings => Set<Holding>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<PriceCache> PriceCaches => Set<PriceCache>();
    public DbSet<LedgerAccount> LedgerAccounts => Set<LedgerAccount>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<Debt> Debts => Set<Debt>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<ChatDocument> ChatDocuments => Set<ChatDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // pgvector's column type exists only on Npgsql. The unit tests run on the in-memory
        // provider, which rejects the Vector type outright and would fail to build the model
        // at all - so the embedding column is mapped only where it can exist.
        var isNpgsql = Database.IsNpgsql();

        if (isNpgsql)
            modelBuilder.HasPostgresExtension("vector");

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly,
            type => type != typeof(ChatDocumentConfiguration));

        modelBuilder.ApplyConfiguration(new ChatDocumentConfiguration(isNpgsql));

        base.OnModelCreating(modelBuilder);
    }
}
