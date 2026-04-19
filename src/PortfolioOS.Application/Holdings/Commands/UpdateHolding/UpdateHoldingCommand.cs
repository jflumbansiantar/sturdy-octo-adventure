using MediatR;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Application.Holdings.Commands.UpdateHolding;

public record UpdateHoldingCommand(
    Guid Id,
    string Name,
    HoldingType Type,
    string SubType,
    Market Market,
    decimal Shares,
    decimal AvgCost) : IRequest;
