using FluentValidation;

namespace PortfolioOS.Application.Debts.Commands.CreateDebt;

public class CreateDebtValidator : AbstractValidator<CreateDebtCommand>
{
    public CreateDebtValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Balance).GreaterThanOrEqualTo(0);
        RuleFor(x => x.InterestRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MinimumPayment).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DueDay).InclusiveBetween(1, 31);
        RuleFor(x => x.Tenor).GreaterThanOrEqualTo(1).When(x => x.Tenor.HasValue);
    }
}
