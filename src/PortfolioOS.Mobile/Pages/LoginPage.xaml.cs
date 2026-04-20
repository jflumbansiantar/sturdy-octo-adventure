using PortfolioOS.Mobile.ViewModels;

namespace PortfolioOS.Mobile.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
