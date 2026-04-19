using MediatR;

namespace PortfolioOS.Application.Settings.Queries.GetSettings;

public record SettingDto(string Key, string Value);

public record GetSettingsQuery : IRequest<List<SettingDto>>;
