namespace PortfolioOS.Web.Models;

public record LedgerAccountModel(
    string Id,
    string Code,
    string Name,
    string Type,
    string NormalBalance,
    decimal OpeningBalance,
    decimal TotalDebits,
    decimal TotalCredits,
    decimal Balance);

public record JournalLineModel(
    Guid Id,
    string AccountId,
    string AccountName,
    decimal Debit,
    decimal Credit);

public record JournalEntryModel(
    string Id,
    DateOnly Date,
    string Description,
    IReadOnlyList<JournalLineModel> Lines,
    DateTimeOffset CreatedAt);

public record LedgerSummaryModel(
    decimal TotalAssets,
    decimal TotalLiabilities,
    decimal TotalEquity,
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal NetWorth,
    IReadOnlyList<LedgerAccountModel> Accounts);
