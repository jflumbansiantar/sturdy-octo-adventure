using PortfolioOS.Mobile.ViewModels;

namespace PortfolioOS.Mobile.Pages;

public partial class AccountPage : ContentPage
{
    public AccountPage(AccountViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Token bisa berganti setelah login ulang, jadi dibaca tiap halaman tampil.
        if (BindingContext is AccountViewModel vm)
            vm.Load();
    }
}
