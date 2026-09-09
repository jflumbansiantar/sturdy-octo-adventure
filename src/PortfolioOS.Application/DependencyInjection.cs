using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PortfolioOS.Application.Chat.Skills;
using PortfolioOS.Application.Common.Behaviors;
using PortfolioOS.Application.Common.Interfaces;
using PortfolioOS.Application.Common.Services;

namespace PortfolioOS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        services.AddScoped<IExchangeRateService, ExchangeRateService>();

        // Chat skills are discovered rather than listed, so adding one is a single new class.
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes()
                     .Where(t => t is { IsAbstract: false, IsInterface: false } &&
                                 typeof(IChatSkill).IsAssignableFrom(t)))
        {
            services.AddScoped(typeof(IChatSkill), type);
        }

        // Injected so "today" is controllable in tests rather than read from the ambient clock.
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
