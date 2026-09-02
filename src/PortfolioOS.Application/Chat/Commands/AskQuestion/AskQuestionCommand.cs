using FluentValidation;
using MediatR;

namespace PortfolioOS.Application.Chat.Commands.AskQuestion;

/// <param name="Question">The user's question, in Indonesian or English.</param>
public record AskQuestionCommand(string Question) : IRequest<ChatAnswer>;

public class AskQuestionValidator : AbstractValidator<AskQuestionCommand>
{
    public AskQuestionValidator()
    {
        RuleFor(x => x.Question)
            .NotEmpty().WithMessage("Pertanyaan tidak boleh kosong.")
            // Long inputs are almost always pasted text rather than a question, and every extra
            // token costs embedding time for a match that will fail the confidence gate anyway.
            .MaximumLength(500).WithMessage("Pertanyaan terlalu panjang, maksimal 500 karakter.");
    }
}
