using MediatR;
using Microsoft.EntityFrameworkCore;
using PortfolioOS.Application.Common.Interfaces;

namespace PortfolioOS.Application.Settings.Queries.GetSettings;

public class GetSettingsHandler : IRequestHandler<GetSettingsQuery, List<SettingDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSettingsHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<SettingDto>> Handle(GetSettingsQuery request, CancellationToken ct)
    {
        var settings = await _context.AppSettings.AsNoTracking().ToListAsync(ct);
        return settings.Select(s => new SettingDto(s.Key, s.Value)).ToList();
    }
}
