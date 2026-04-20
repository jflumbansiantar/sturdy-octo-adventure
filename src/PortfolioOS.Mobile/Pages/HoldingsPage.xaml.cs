using PortfolioOS.Mobile.ViewModels;

namespace PortfolioOS.Mobile.Pages;

public partial class HoldingsPage : ContentPage
{
    public HoldingsPage(HoldingsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is HoldingsViewModel vm)
            await vm.LoadCommand.ExecuteAsync(null);
    }
}
