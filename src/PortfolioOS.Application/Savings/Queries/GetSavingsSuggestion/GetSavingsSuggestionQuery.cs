using MediatR;

namespace PortfolioOS.Application.Savings.Queries.GetSavingsSuggestion;

/// <param name="Months">
/// How many months back to read, counting the current one. Clamped to 3-24: fewer than three
/// says nothing about a habit, more than two years describes a life the user no longer lives.
/// </param>
public record GetSavingsSuggestionQuery(int Months = 6) : IRequest<SavingsSuggestionDto>;
