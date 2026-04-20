using PortfolioOS.Mobile.Services;

namespace PortfolioOS.Mobile;

public partial class App : Application
{
    public App(AuthService auth)
    {
        InitializeComponent();
        MainPage = new AppShell();
    }
}
