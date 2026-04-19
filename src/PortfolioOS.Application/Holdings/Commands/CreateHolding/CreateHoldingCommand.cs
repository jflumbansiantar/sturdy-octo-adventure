using MediatR;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Application.Holdings.Commands.CreateHolding;

public record CreateHoldingCommand(
    string Ticker,
    string Name,
    HoldingType Type,
    string SubType,
    Market Market,
    decimal Shares,
    decimal AvgCost) : IRequest<Guid>;
