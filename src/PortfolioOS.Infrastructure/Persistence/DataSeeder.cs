using Microsoft.EntityFrameworkCore;
using PortfolioOS.Domain.Entities;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Infrastructure.Persistence;

public static class DataSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db)
    {
        await SeedAppSettingsAsync(db);
        await SeedLedgerAccountsAsync(db);
        await SeedHoldingsAsync(db);
        await SeedPriceCachesAsync(db);
        await SeedDebtsAsync(db);
        await SeedTransactionsAsync(db);
        await SeedJournalEntriesAsync(db);
    }

    // ─────────────────────────────────────────────────────────────
    // App Settings
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedAppSettingsAsync(ApplicationDbContext db)
    {
        if (await db.AppSettings.AnyAsync()) return;

        var now = DateTimeOffset.UtcNow;
        db.AppSettings.AddRange(
            new AppSetting { Key = "default_currency",    Value = "\"IDR\"",   CreatedAt = now, UpdatedAt = now },
            new AppSetting { Key = "portfolio_benchmark", Value = "\"IHSG\"",  CreatedAt = now, UpdatedAt = now },
            new AppSetting { Key = "display_name",        Value = "\"Admin\"", CreatedAt = now, UpdatedAt = now }
        );
        await db.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────
    // Ledger Accounts (Chart of Accounts)
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedLedgerAccountsAsync(ApplicationDbContext db)
    {
        if (await db.LedgerAccounts.AnyAsync()) return;

        var now = DateTimeOffset.UtcNow;
        db.LedgerAccounts.AddRange(
            // Assets
            new LedgerAccount { Id = "A1000", Code = "1000", Name = "Kas",                     Type = AccountType.Asset,     NormalBalance = NormalBalanceType.Debit,  OpeningBalance = 5_000_000m,   CreatedAt = now, UpdatedAt = now },
            new LedgerAccount { Id = "A1100", Code = "1100", Name = "Bank BCA",                 Type = AccountType.Asset,     NormalBalance = NormalBalanceType.Debit,  OpeningBalance = 25_000_000m,  CreatedAt = now, UpdatedAt = now },
            new LedgerAccount { Id = "A1200", Code = "1200", Name = "Bank Mandiri",             Type = AccountType.Asset,     NormalBalance = NormalBalanceType.Debit,  OpeningBalance = 10_000_000m,  CreatedAt = now, UpdatedAt = now },
            new LedgerAccount { Id = "A1300", Code = "1300", Name = "Portofolio Investasi",     Type = AccountType.Asset,     NormalBalance = NormalBalanceType.Debit,  OpeningBalance = 150_000_000m, CreatedAt = now, UpdatedAt = now },
            new LedgerAccount { Id = "A1400", Code = "1400", Name = "Piutang",                  Type = AccountType.Asset,     NormalBalance = NormalBalanceType.Debit,  OpeningBalance = 0m,           CreatedAt = now, UpdatedAt = now },
            // Liabilities
            new LedgerAccount { Id = "L2000", Code = "2000", Name = "Kartu Kredit BCA",         Type = AccountType.Liability, NormalBalance = NormalBalanceType.Credit, OpeningBalance = 5_200_000m,   CreatedAt = now, UpdatedAt = now },
            new LedgerAccount { Id = "L2100", Code = "2100", Name = "KTA Mandiri",              Type = AccountType.Liability, NormalBalance = NormalBalanceType.Credit, OpeningBalance = 12_000_000m,  CreatedAt = now, UpdatedAt = now },
            new LedgerAccount { Id = "L2200", Code = "2200", Name = "KPR BNI",                  Type = AccountType.Liability, NormalBalance = NormalBalanceType.Credit, OpeningBalance = 450_000_000m, CreatedAt = now, UpdatedAt = now },
            // Equity
            new LedgerAccount { Id = "E3000", Code = "3000", Name = "Modal Awal",               Type = AccountType.Equity,    NormalBalance = NormalBalanceType.Credit, OpeningBalance = 0m,           CreatedAt = now, UpdatedAt = now },
            new LedgerAccount { Id = "E3100", Code = "3100", Name = "Laba Ditahan",             Type = AccountType.Equity,    NormalBalance = NormalBalanceType.Credit, OpeningBalance = 0m,           CreatedAt = now, UpdatedAt = now },
            // Income
            new LedgerAccount { Id = "I4000", Code = "4000", Name = "Pendapatan Gaji",         Type = AccountType.Income,    NormalBalance = NormalBalanceType.Credit, OpeningBalance = 0m,           CreatedAt = now, UpdatedAt = now },
            new LedgerAccount { Id = "I4100", Code = "4100", Name = "Pendapatan Dividen",      Type = AccountType.Income,    NormalBalance = NormalBalanceType.Credit, OpeningBalance = 0m,           CreatedAt = now, UpdatedAt = now },
            new LedgerAccount { Id = "I4200", Code = "4200", Name = "Keuntungan Investasi",    Type = AccountType.Income,    NormalBalance = NormalBalanceType.Credit, OpeningBalance = 0m,           CreatedAt = now, UpdatedAt = now },
            // Expenses
            new LedgerAccount { Id = "X5000", Code = "5000", Name = "Kebutuhan Rumah Tangga",  Type = AccountType.Expense,   NormalBalance = NormalBalanceType.Debit,  OpeningBalance = 0m,           CreatedAt = now, UpdatedAt = now },
            new LedgerAccount { Id = "X5100", Code = "5100", Name = "Transportasi",             Type = AccountType.Expense,   NormalBalance = NormalBalanceType.Debit,  OpeningBalance = 0m,           CreatedAt = now, UpdatedAt = now },
            new LedgerAccount { Id = "X5200", Code = "5200", Name = "Makan & Hiburan",          Type = AccountType.Expense,   NormalBalance = NormalBalanceType.Debit,  OpeningBalance = 0m,           CreatedAt = now, UpdatedAt = now },
            new LedgerAccount { Id = "X5300", Code = "5300", Name = "Cicilan Utang",            Type = AccountType.Expense,   NormalBalance = NormalBalanceType.Debit,  OpeningBalance = 0m,           CreatedAt = now, UpdatedAt = now },
            new LedgerAccount { Id = "X5400", Code = "5400", Name = "Langganan & Utilitas",    Type = AccountType.Expense,   NormalBalance = NormalBalanceType.Debit,  OpeningBalance = 0m,           CreatedAt = now, UpdatedAt = now }
        );
        await db.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────
    // Holdings
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedHoldingsAsync(ApplicationDbContext db)
    {
        if (await db.Holdings.AnyAsync()) return;

        var now = DateTimeOffset.UtcNow;
        db.Holdings.AddRange(
            // US Stocks
            new Holding { Id = Guid.NewGuid(), Ticker = "AAPL",  Name = "Apple Inc.",                    Type = HoldingType.Stock,      SubType = "Large Cap",        Market = Market.US, Shares = 15m,       AvgCost = 162.40m,   CreatedAt = now, UpdatedAt = now },
            new Holding { Id = Guid.NewGuid(), Ticker = "MSFT",  Name = "Microsoft Corporation",         Type = HoldingType.Stock,      SubType = "Large Cap",        Market = Market.US, Shares = 8m,        AvgCost = 310.50m,   CreatedAt = now, UpdatedAt = now },
            new Holding { Id = Guid.NewGuid(), Ticker = "NVDA",  Name = "NVIDIA Corporation",            Type = HoldingType.Stock,      SubType = "Technology",       Market = Market.US, Shares = 5m,        AvgCost = 480.00m,   CreatedAt = now, UpdatedAt = now },
            // US ETF
            new Holding { Id = Guid.NewGuid(), Ticker = "EIDO",  Name = "iShares MSCI Indonesia ETF",    Type = HoldingType.ETF,        SubType = "Country",          Market = Market.US, Shares = 200m,      AvgCost = 20.80m,    CreatedAt = now, UpdatedAt = now },
            new Holding { Id = Guid.NewGuid(), Ticker = "VTI",   Name = "Vanguard Total Stock Market",  Type = HoldingType.ETF,        SubType = "Broad Market",     Market = Market.US, Shares = 12m,       AvgCost = 225.00m,   CreatedAt = now, UpdatedAt = now },
            // ID Stocks
            new Holding { Id = Guid.NewGuid(), Ticker = "BBCA",  Name = "Bank Central Asia Tbk",        Type = HoldingType.Stock,      SubType = "Perbankan",        Market = Market.ID, Shares = 1000m,     AvgCost = 9200m,     CreatedAt = now, UpdatedAt = now },
            new Holding { Id = Guid.NewGuid(), Ticker = "TLKM",  Name = "Telkom Indonesia Tbk",         Type = HoldingType.Stock,      SubType = "Telekomunikasi",   Market = Market.ID, Shares = 5000m,     AvgCost = 3450m,     CreatedAt = now, UpdatedAt = now },
            new Holding { Id = Guid.NewGuid(), Ticker = "BBRI",  Name = "Bank Rakyat Indonesia Tbk",    Type = HoldingType.Stock,      SubType = "Perbankan",        Market = Market.ID, Shares = 2000m,     AvgCost = 4800m,     CreatedAt = now, UpdatedAt = now },
            new Holding { Id = Guid.NewGuid(), Ticker = "ASII",  Name = "Astra International Tbk",     Type = HoldingType.Stock,      SubType = "Konglomerat",      Market = Market.ID, Shares = 3000m,     AvgCost = 5200m,     CreatedAt = now, UpdatedAt = now },
            // Crypto
            new Holding { Id = Guid.NewGuid(), Ticker = "BTC",   Name = "Bitcoin",                      Type = HoldingType.Crypto,     SubType = "Layer 1",          Market = Market.US, Shares = 0.08m,     AvgCost = 42000.00m, CreatedAt = now, UpdatedAt = now },
            new Holding { Id = Guid.NewGuid(), Ticker = "ETH",   Name = "Ethereum",                     Type = HoldingType.Crypto,     SubType = "Layer 1",          Market = Market.US, Shares = 1.25m,     AvgCost = 2200.00m,  CreatedAt = now, UpdatedAt = now },
            // Reksa Dana
            new Holding { Id = Guid.NewGuid(), Ticker = "RDPU1", Name = "Reksa Dana Pasar Uang BCA",    Type = HoldingType.MutualFund, SubType = "Pasar Uang",       Market = Market.ID, Shares = 50000m,    AvgCost = 1000m,     CreatedAt = now, UpdatedAt = now },
            new Holding { Id = Guid.NewGuid(), Ticker = "RDSH1", Name = "Reksa Dana Saham Syariah",     Type = HoldingType.MutualFund, SubType = "Saham Syariah",    Market = Market.ID, Shares = 20000m,    AvgCost = 1500m,     CreatedAt = now, UpdatedAt = now }
        );
        await db.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────
    // Price Caches
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedPriceCachesAsync(ApplicationDbContext db)
    {
        if (await db.PriceCaches.AnyAsync()) return;

        var now = DateTimeOffset.UtcNow;
        db.PriceCaches.AddRange(
            new PriceCache { Ticker = "AAPL",  Currency = CurrencyType.USD, CurrentPrice = 185.50m,    PreviousClose = 183.20m,    UpdatedAt = now },
            new PriceCache { Ticker = "MSFT",  Currency = CurrencyType.USD, CurrentPrice = 378.85m,    PreviousClose = 376.40m,    UpdatedAt = now },
            new PriceCache { Ticker = "NVDA",  Currency = CurrencyType.USD, CurrentPrice = 875.40m,    PreviousClose = 858.00m,    UpdatedAt = now },
            new PriceCache { Ticker = "EIDO",  Currency = CurrencyType.USD, CurrentPrice = 22.50m,     PreviousClose = 22.30m,     UpdatedAt = now },
            new PriceCache { Ticker = "VTI",   Currency = CurrencyType.USD, CurrentPrice = 238.10m,    PreviousClose = 235.80m,    UpdatedAt = now },
            new PriceCache { Ticker = "BBCA",  Currency = CurrencyType.IDR, CurrentPrice = 9850m,      PreviousClose = 9800m,      UpdatedAt = now },
            new PriceCache { Ticker = "TLKM",  Currency = CurrencyType.IDR, CurrentPrice = 3620m,      PreviousClose = 3650m,      UpdatedAt = now },
            new PriceCache { Ticker = "BBRI",  Currency = CurrencyType.IDR, CurrentPrice = 5025m,      PreviousClose = 4975m,      UpdatedAt = now },
            new PriceCache { Ticker = "ASII",  Currency = CurrencyType.IDR, CurrentPrice = 5400m,      PreviousClose = 5350m,      UpdatedAt = now },
            new PriceCache { Ticker = "BTC",   Currency = CurrencyType.USD, CurrentPrice = 67_250.00m, PreviousClose = 65_800.00m, UpdatedAt = now },
            new PriceCache { Ticker = "ETH",   Currency = CurrencyType.USD, CurrentPrice = 3_480.00m,  PreviousClose = 3_350.00m,  UpdatedAt = now },
            new PriceCache { Ticker = "RDPU1", Currency = CurrencyType.IDR, CurrentPrice = 1020m,      PreviousClose = 1019m,      UpdatedAt = now },
            new PriceCache { Ticker = "RDSH1", Currency = CurrencyType.IDR, CurrentPrice = 1680m,      PreviousClose = 1660m,      UpdatedAt = now }
        );
        await db.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────
    // Debts
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedDebtsAsync(ApplicationDbContext db)
    {
        if (await db.Debts.AnyAsync()) return;

        var now = DateTimeOffset.UtcNow;
        db.Debts.AddRange(
            new Debt
            {
                Id = Guid.NewGuid(),
                Name = "Kartu Kredit BCA Platinum",
                Type = DebtType.CreditCard,
                Balance = 5_200_000m,
                InterestRate = 27m,               // 27% per annum
                MonthlyInterestRate = 2.25m,
                MinimumPayment = 500_000m,
                DueDay = 15,
                Currency = CurrencyType.IDR,
                DebtApp = "myBCA",
                Notes = "Digunakan untuk kebutuhan sehari-hari. Bayar full setiap bulan jika memungkinkan.",
                Status = DebtStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Debt
            {
                Id = Guid.NewGuid(),
                Name = "KTA Mandiri",
                Type = DebtType.PersonalLoan,
                Balance = 12_000_000m,
                InterestRate = 11.4m,             // 11.4% per annum
                MonthlyInterestRate = 0.95m,
                Tenor = 24,
                MinimumPayment = 615_000m,
                DueDay = 10,
                Currency = CurrencyType.IDR,
                DebtApp = "Livin by Mandiri",
                Notes = "Tenor 24 bulan. Bulan ke-8 dari 24.",
                Status = DebtStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Debt
            {
                Id = Guid.NewGuid(),
                Name = "KPR BNI — Rumah Ciputat",
                Type = DebtType.Mortgage,
                Balance = 450_000_000m,
                InterestRate = 8.5m,              // 8.5% per annum
                Tenor = 180,                       // 15 tahun
                MinimumPayment = 4_500_000m,
                DueDay = 5,
                Currency = CurrencyType.IDR,
                DebtApp = "BNI Mobile Banking",
                Notes = "KPR 15 tahun. Fixed 3 tahun pertama, floating setelahnya.",
                Status = DebtStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Debt
            {
                Id = Guid.NewGuid(),
                Name = "Cicilan HP Samsung S24",
                Type = DebtType.Other,
                Balance = 0m,
                InterestRate = 0m,
                Tenor = 12,
                MinimumPayment = 0m,
                DueDay = 20,
                Currency = CurrencyType.IDR,
                DebtApp = "BCA Mobile",
                Notes = "0% bunga. Sudah lunas bulan Maret 2026.",
                Status = DebtStatus.Lunas,
                CreatedAt = now,
                UpdatedAt = now
            }
        );
        await db.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────
    // Transactions
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedTransactionsAsync(ApplicationDbContext db)
    {
        if (await db.Transactions.AnyAsync()) return;

        var now = DateTimeOffset.UtcNow;
        db.Transactions.AddRange(
            // Income
            new Transaction { Id = Guid.NewGuid(), Date = new DateOnly(2026, 4, 1),  Category = TransactionCategory.Income,  Name = "Gaji April",           Type = "Salary",       Total = 18_000_000m,  CreatedAt = now, UpdatedAt = now },
            new Transaction { Id = Guid.NewGuid(), Date = new DateOnly(2026, 3, 1),  Category = TransactionCategory.Income,  Name = "Gaji Maret",           Type = "Salary",       Total = 18_000_000m,  CreatedAt = now, UpdatedAt = now },
            new Transaction { Id = Guid.NewGuid(), Date = new DateOnly(2026, 4, 10), Category = TransactionCategory.Income,  Name = "Dividen BBCA Q1",      Type = "Dividend",     Total = 230_000m,     CreatedAt = now, UpdatedAt = now },
            new Transaction { Id = Guid.NewGuid(), Date = new DateOnly(2026, 3, 20), Category = TransactionCategory.Income,  Name = "Dividen TLKM Q1",      Type = "Dividend",     Total = 185_000m,     CreatedAt = now, UpdatedAt = now },
            // Expenses
            new Transaction { Id = Guid.NewGuid(), Date = new DateOnly(2026, 4, 5),  Category = TransactionCategory.Expense, Name = "Belanja Bulanan",       Type = "Groceries",    Total = 1_500_000m,   CreatedAt = now, UpdatedAt = now },
            new Transaction { Id = Guid.NewGuid(), Date = new DateOnly(2026, 4, 8),  Category = TransactionCategory.Expense, Name = "Bensin & Parkir",       Type = "Transport",    Total = 450_000m,     CreatedAt = now, UpdatedAt = now },
            new Transaction { Id = Guid.NewGuid(), Date = new DateOnly(2026, 4, 12), Category = TransactionCategory.Expense, Name = "Makan & Nongkrong",     Type = "Food",         Total = 780_000m,     CreatedAt = now, UpdatedAt = now },
            new Transaction { Id = Guid.NewGuid(), Date = new DateOnly(2026, 4, 2),  Category = TransactionCategory.Expense, Name = "Netflix + Spotify",     Type = "Subscription", Total = 218_000m,     CreatedAt = now, UpdatedAt = now },
            // Debt payments
            new Transaction { Id = Guid.NewGuid(), Date = new DateOnly(2026, 4, 5),  Category = TransactionCategory.Debt,    Name = "Bayar KPR BNI",         Type = "Debt Payment", Total = 4_500_000m,   CreatedAt = now, UpdatedAt = now },
            new Transaction { Id = Guid.NewGuid(), Date = new DateOnly(2026, 4, 10), Category = TransactionCategory.Debt,    Name = "Cicilan KTA Mandiri",   Type = "Debt Payment", Total = 615_000m,     CreatedAt = now, UpdatedAt = now },
            new Transaction { Id = Guid.NewGuid(), Date = new DateOnly(2026, 4, 15), Category = TransactionCategory.Debt,    Name = "Tagihan KK BCA",        Type = "Debt Payment", Total = 3_200_000m,   CreatedAt = now, UpdatedAt = now },
            // Stock transactions (US)
            new Transaction { Id = Guid.NewGuid(), Date = new DateOnly(2026, 3, 15), Category = TransactionCategory.Stock,   Name = "AAPL",  Type = "Buy",  Total = 1_786.40m,    Market = Market.US, Shares = 11m, Price = 162.40m, CreatedAt = now, UpdatedAt = now },
            new Transaction { Id = Guid.NewGuid(), Date = new DateOnly(2026, 4, 2),  Category = TransactionCategory.Stock,   Name = "AAPL",  Type = "Buy",  Total = 741.00m,      Market = Market.US, Shares = 4m,  Price = 185.25m, CreatedAt = now, UpdatedAt = now },
            new Transaction { Id = Guid.NewGuid(), Date = new DateOnly(2026, 3, 5),  Category = TransactionCategory.Stock,   Name = "NVDA",  Type = "Buy",  Total = 2_400.00m,    Market = Market.US, Shares = 5m,  Price = 480.00m, CreatedAt = now, UpdatedAt = now },
            // Stock transactions (ID)
            new Transaction { Id = Guid.NewGuid(), Date = new DateOnly(2026, 2, 10), Category = TransactionCategory.Stock,   Name = "BBCA",  Type = "Buy",  Total = 9_200_000m,   Market = Market.ID, Shares = 1000m, Price = 9200m, CreatedAt = now, UpdatedAt = now },
            new Transaction { Id = Guid.NewGuid(), Date = new DateOnly(2026, 3, 22), Category = TransactionCategory.Stock,   Name = "TLKM",  Type = "Buy",  Total = 17_250_000m,  Market = Market.ID, Shares = 5000m, Price = 3450m, CreatedAt = now, UpdatedAt = now },
            new Transaction { Id = Guid.NewGuid(), Date = new DateOnly(2026, 1, 18), Category = TransactionCategory.Stock,   Name = "BBRI",  Type = "Buy",  Total = 9_600_000m,   Market = Market.ID, Shares = 2000m, Price = 4800m, CreatedAt = now, UpdatedAt = now },
            new Transaction { Id = Guid.NewGuid(), Date = new DateOnly(2026, 4, 1),  Category = TransactionCategory.Stock,   Name = "ASII",  Type = "Buy",  Total = 15_600_000m,  Market = Market.ID, Shares = 3000m, Price = 5200m, CreatedAt = now, UpdatedAt = now },
            // Crypto
            new Transaction { Id = Guid.NewGuid(), Date = new DateOnly(2026, 1, 5),  Category = TransactionCategory.Stock,   Name = "BTC",   Type = "Buy",  Total = 3_360.00m,    Market = Market.US, Shares = 0.08m, Price = 42000m, CreatedAt = now, UpdatedAt = now },
            new Transaction { Id = Guid.NewGuid(), Date = new DateOnly(2026, 2, 20), Category = TransactionCategory.Stock,   Name = "ETH",   Type = "Buy",  Total = 2_750.00m,    Market = Market.US, Shares = 1.25m, Price = 2200m,  CreatedAt = now, UpdatedAt = now }
        );
        await db.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────
    // Journal Entries (double-entry bookkeeping examples)
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedJournalEntriesAsync(ApplicationDbContext db)
    {
        if (await db.JournalEntries.AnyAsync()) return;

        var now = DateTimeOffset.UtcNow;

        // JE001 — Gaji April masuk ke Bank BCA
        var je001 = new JournalEntry
        {
            Id = "JE001",
            Date = new DateOnly(2026, 4, 1),
            Description = "Penerimaan gaji bulan April 2026",
            CreatedAt = now,
            UpdatedAt = now,
            Lines =
            [
                new JournalLine { Id = Guid.NewGuid(), AccountId = "A1100", Debit = 18_000_000m, Credit = 0m },   // Debit Bank BCA
                new JournalLine { Id = Guid.NewGuid(), AccountId = "I4000", Debit = 0m, Credit = 18_000_000m }    // Credit Pendapatan Gaji
            ]
        };

        // JE002 — Bayar KPR BNI dari Bank BCA
        var je002 = new JournalEntry
        {
            Id = "JE002",
            Date = new DateOnly(2026, 4, 5),
            Description = "Pembayaran cicilan KPR BNI bulan April 2026",
            CreatedAt = now,
            UpdatedAt = now,
            Lines =
            [
                new JournalLine { Id = Guid.NewGuid(), AccountId = "L2200", Debit = 4_500_000m, Credit = 0m },   // Debit KPR BNI (pokok)
                new JournalLine { Id = Guid.NewGuid(), AccountId = "A1100", Debit = 0m, Credit = 4_500_000m }    // Credit Bank BCA
            ]
        };

        // JE003 — Dividen BBCA diterima
        var je003 = new JournalEntry
        {
            Id = "JE003",
            Date = new DateOnly(2026, 4, 10),
            Description = "Penerimaan dividen BBCA Q1 2026",
            CreatedAt = now,
            UpdatedAt = now,
            Lines =
            [
                new JournalLine { Id = Guid.NewGuid(), AccountId = "A1100", Debit = 230_000m, Credit = 0m },     // Debit Bank BCA
                new JournalLine { Id = Guid.NewGuid(), AccountId = "I4100", Debit = 0m, Credit = 230_000m }      // Credit Pendapatan Dividen
            ]
        };

        // JE004 — Belanja bulanan via KK BCA
        var je004 = new JournalEntry
        {
            Id = "JE004",
            Date = new DateOnly(2026, 4, 5),
            Description = "Belanja kebutuhan bulanan menggunakan kartu kredit BCA",
            CreatedAt = now,
            UpdatedAt = now,
            Lines =
            [
                new JournalLine { Id = Guid.NewGuid(), AccountId = "X5000", Debit = 1_500_000m, Credit = 0m },   // Debit Kebutuhan RT
                new JournalLine { Id = Guid.NewGuid(), AccountId = "L2000", Debit = 0m, Credit = 1_500_000m }    // Credit KK BCA
            ]
        };

        // JE005 — Pembelian saham AAPL
        var je005 = new JournalEntry
        {
            Id = "JE005",
            Date = new DateOnly(2026, 4, 2),
            Description = "Pembelian 4 lembar saham AAPL di harga USD 185.25",
            CreatedAt = now,
            UpdatedAt = now,
            Lines =
            [
                new JournalLine { Id = Guid.NewGuid(), AccountId = "A1300", Debit = 741m, Credit = 0m },         // Debit Portofolio Investasi
                new JournalLine { Id = Guid.NewGuid(), AccountId = "A1100", Debit = 0m, Credit = 741m }          // Credit Bank BCA
            ]
        };

        db.JournalEntries.AddRange(je001, je002, je003, je004, je005);
        await db.SaveChangesAsync();
    }
}
