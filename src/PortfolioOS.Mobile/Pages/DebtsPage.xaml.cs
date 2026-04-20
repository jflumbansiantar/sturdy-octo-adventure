using PortfolioOS.Mobile.ViewModels;

namespace PortfolioOS.Mobile.Pages;

public partial class DebtsPage : ContentPage
{
    public DebtsPage(DebtsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is DebtsViewModel vm)
            await vm.LoadCommand.ExecuteAsync(null);
    }
}
