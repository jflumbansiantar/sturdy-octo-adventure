using MediatR;

namespace PortfolioOS.Application.Holdings.Commands.DeleteHolding;

public record DeleteHoldingCommand(Guid Id) : IRequest;
