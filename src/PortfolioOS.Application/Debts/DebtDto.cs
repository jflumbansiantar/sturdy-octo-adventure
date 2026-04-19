namespace PortfolioOS.Application.Debts;

public record DebtDto(
    Guid Id,
    string Name,
    string Type,
    decimal Balance,
    decimal InterestRate,
    decimal? MonthlyInterestRate,
    int? Tenor,
    decimal MinimumPayment,
    int DueDay,
    string Currency,
    string DebtApp,
    string Notes,
    string Status,
    decimal TotalPaid,
    int MonthsPaid,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
