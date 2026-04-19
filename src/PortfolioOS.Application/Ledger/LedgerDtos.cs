namespace PortfolioOS.Application.Ledger;

public record LedgerAccountDto(
    string Id,
    string Code,
    string Name,
    string Type,
    string NormalBalance,
    decimal OpeningBalance,
    decimal TotalDebits,
    decimal TotalCredits,
    decimal Balance);

public record JournalLineDto(
    Guid Id,
    string AccountId,
    string AccountName,
    decimal Debit,
    decimal Credit);

public record JournalEntryDto(
    string Id,
    DateOnly Date,
    string Description,
    IReadOnlyList<JournalLineDto> Lines,
    DateTimeOffset CreatedAt);

public record LedgerSummaryDto(
    decimal TotalAssets,
    decimal TotalLiabilities,
    decimal TotalEquity,
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal NetWorth,
    IReadOnlyList<LedgerAccountDto> Accounts);
