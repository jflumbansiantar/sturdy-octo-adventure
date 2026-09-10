using Microsoft.Extensions.Logging;
using PortfolioOS.Mobile.Pages;
using PortfolioOS.Mobile.Services;
using PortfolioOS.Mobile.Services.Ocr;
using PortfolioOS.Mobile.ViewModels;
using PortfolioOS.Shared.Scanning;

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
        var apiBase = "http://localhost:5243"; // via `adb reverse tcp:5243 tcp:5243` → API Docker di PC

        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton(_ => new HttpClient(new HttpClientHandler
        {
            // Allow self-signed certs in dev
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        })
        { BaseAddress = new Uri(apiBase) });
        builder.Services.AddSingleton<ApiClient>();

        // Document scanning. OcrService resolves to the Android or iOS implementation in
        // Platforms/; ReceiptScanner is the platform-neutral parsing engine from Shared.
        builder.Services.AddSingleton<IOcrService, OcrService>();
        builder.Services.AddSingleton<ReceiptScanner>();

        // ViewModels
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<HoldingsViewModel>();
        builder.Services.AddTransient<TransactionsViewModel>();
        builder.Services.AddTransient<ScanReviewViewModel>();
        builder.Services.AddTransient<DebtsViewModel>();
        builder.Services.AddTransient<AccountViewModel>();

        // Pages
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<HoldingsPage>();
        builder.Services.AddTransient<TransactionsPage>();
        builder.Services.AddTransient<ScanReviewPage>();
        builder.Services.AddTransient<DebtsPage>();
        builder.Services.AddTransient<AccountPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
