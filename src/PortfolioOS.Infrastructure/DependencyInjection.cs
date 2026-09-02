using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PortfolioOS.Application.Chat;
using PortfolioOS.Application.Common.Interfaces;
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
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                    npgsql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null);
                    npgsql.UseVector();
                }));

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
