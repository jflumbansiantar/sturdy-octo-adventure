using PortfolioOS.Mobile.ViewModels;

namespace PortfolioOS.Mobile.Pages;

public partial class DashboardPage : ContentPage
{
    public DashboardPage(DashboardViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is DashboardViewModel vm)
            await vm.LoadCommand.ExecuteAsync(null);
    }
}
