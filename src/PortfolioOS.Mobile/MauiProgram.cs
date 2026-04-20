using Microsoft.Extensions.Logging;
using PortfolioOS.Mobile.Pages;
using PortfolioOS.Mobile.Services;
using PortfolioOS.Mobile.ViewModels;

namespace PortfolioOS.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // API base URL — override via appsettings or environment variable for prod
        var apiBase = "https://10.0.2.2:7195"; // Android emulator → localhost

        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton(_ => new HttpClient(new HttpClientHandler
        {
            // Allow self-signed certs in dev
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        })
        { BaseAddress = new Uri(apiBase) });
        builder.Services.AddSingleton<ApiClient>();

        // ViewModels
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<HoldingsViewModel>();
        builder.Services.AddTransient<TransactionsViewModel>();
        builder.Services.AddTransient<DebtsViewModel>();

        // Pages
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<HoldingsPage>();
        builder.Services.AddTransient<TransactionsPage>();
        builder.Services.AddTransient<DebtsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
