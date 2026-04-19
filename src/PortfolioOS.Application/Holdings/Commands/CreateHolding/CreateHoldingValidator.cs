using FluentValidation;

namespace PortfolioOS.Application.Holdings.Commands.CreateHolding;

public class CreateHoldingValidator : AbstractValidator<CreateHoldingCommand>
{
    public CreateHoldingValidator()
    {
        RuleFor(x => x.Ticker).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Shares).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AvgCost).GreaterThanOrEqualTo(0);
    }
}
