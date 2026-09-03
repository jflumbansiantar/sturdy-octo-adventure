using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PortfolioOS.Application.Chat;
using PortfolioOS.Application.Common.Interfaces;
using PortfolioOS.Infrastructure.Demo;
using PortfolioOS.Infrastructure.Persistence;
using PortfolioOS.Infrastructure.Services;
using PortfolioOS.Infrastructure.Services.Chat;

namespace PortfolioOS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")!;

        services.Configure<DemoOptions>(configuration.GetSection(DemoOptions.SectionName));
        services.AddScoped<DemoSessionContext>();
        services.AddSingleton<IDemoConnectionString>(new DemoConnectionString(connectionString));
        services.AddSingleton(new DemoSessionStore(connectionString));
        services.AddSingleton<DemoSessionManager>();

        // Options are built per scope (the IServiceProvider overload), which is what lets a
        // demo request be pointed at its own schema without a second DbContext type: by the
        // time a controller resolves this, the API's demo middleware has bound the scope's
        // DemoSessionContext. Everything else - the owner's requests, background services -
        // leaves it unbound and gets `public`.
        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            var demo = sp.GetRequiredService<DemoSessionContext>();

            options.UseNpgsql(
                demo.Schema is null
                    ? connectionString
                    : DemoSchema.ConnectionStringFor(connectionString, demo.Schema),
                npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                    npgsql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null);
                    npgsql.UseVector();
                });
        });

        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<ApplicationDbContext>());

        services.AddHttpClient<IMarketDataService, YahooFinanceMarketDataService>();

        // Singleton: the ONNX session is expensive to build and safe to share.
        services.AddSingleton<IEmbeddingService, OnnxEmbeddingService>();
        services.AddScoped<IChatRetriever, PgVectorChatRetriever>();
        services.AddScoped<IChatIndexService, ChatIndexService>();

        return services;
    }
}
