using PortfolioOS.Mobile.ViewModels;

namespace PortfolioOS.Mobile.Pages;

public partial class TransactionsPage : ContentPage
{
    public TransactionsPage(TransactionsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is TransactionsViewModel vm)
            await vm.LoadCommand.ExecuteAsync(null);
    }
}
