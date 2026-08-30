using PortfolioOS.Mobile.ViewModels;

namespace PortfolioOS.Mobile.Pages;

public partial class ScanReviewPage : ContentPage
{
    public ScanReviewPage(ScanReviewViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Confirming the ticker against the user's holdings needs the API, so it happens here
        // rather than during parsing - the scanner itself stays free of I/O.
        if (BindingContext is ScanReviewViewModel vm)
            await vm.MatchHoldingCommand.ExecuteAsync(null);
    }
}
