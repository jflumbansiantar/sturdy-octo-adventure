using MediatR;

namespace PortfolioOS.Application.Settings.Commands.UpdateSetting;

public record UpdateSettingCommand(string Key, string Value) : IRequest;
