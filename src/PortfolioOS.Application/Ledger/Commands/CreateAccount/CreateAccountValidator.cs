using FluentValidation;

namespace PortfolioOS.Application.Ledger.Commands.CreateAccount;

public class CreateAccountValidator : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountValidator()
    {
        RuleFor(x => x.Id).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.OpeningBalance).GreaterThanOrEqualTo(0);
    }
}
