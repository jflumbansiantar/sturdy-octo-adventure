using PortfolioOS.Mobile.Pages;
using PortfolioOS.Mobile.Services;

namespace PortfolioOS.Mobile;

public partial class AppShell : Shell
{
    private readonly AuthService _auth;

    public AppShell(AuthService auth)
    {
        InitializeComponent();
        _auth = auth;

        // Pushed on top of the tab bar rather than being a tab of its own, so it needs an
        // explicit route registration.
        Routing.RegisterRoute(nameof(ScanReviewPage), typeof(ScanReviewPage));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Route to the right screen on launch
        if (_auth.IsAuthenticated())
            await GoToAsync("//dashboard");
        else
            await GoToAsync("//login");
    }
}
