using FluentValidation;

namespace PortfolioOS.Application.Holdings.Commands.UpdateHolding;

public class UpdateHoldingValidator : AbstractValidator<UpdateHoldingCommand>
{
    public UpdateHoldingValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Shares).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AvgCost).GreaterThanOrEqualTo(0);
    }
}
