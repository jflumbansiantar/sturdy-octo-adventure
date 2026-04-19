using FluentValidation;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Application.Transactions.Commands.CreateTransaction;

public class CreateTransactionValidator : AbstractValidator<CreateTransactionCommand>
{
    public CreateTransactionValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Type).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Total).GreaterThanOrEqualTo(0);

        When(x => x.Category == TransactionCategory.Stock, () =>
        {
            RuleFor(x => x.Market).NotNull().WithMessage("Market is required for STOCK transactions.");
            RuleFor(x => x.Shares).NotNull().GreaterThan(0).WithMessage("Shares required for STOCK transactions.");
            RuleFor(x => x.Price).NotNull().GreaterThan(0).WithMessage("Price required for STOCK transactions.");
        });
    }
}
