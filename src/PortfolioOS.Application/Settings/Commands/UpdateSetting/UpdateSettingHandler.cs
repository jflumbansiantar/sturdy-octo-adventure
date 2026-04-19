using MediatR;
using Microsoft.EntityFrameworkCore;
using PortfolioOS.Application.Common.Interfaces;
using PortfolioOS.Domain.Entities;

namespace PortfolioOS.Application.Settings.Commands.UpdateSetting;

public class UpdateSettingHandler : IRequestHandler<UpdateSettingCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateSettingHandler(IApplicationDbContext context) => _context = context;

    public async Task Handle(UpdateSettingCommand cmd, CancellationToken ct)
    {
        var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == cmd.Key, ct);

        if (setting is null)
        {
            setting = new AppSetting
            {
                Key       = cmd.Key,
                Value     = cmd.Value,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _context.AppSettings.Add(setting);
        }
        else
        {
            setting.Value     = cmd.Value;
            setting.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
    }
}
