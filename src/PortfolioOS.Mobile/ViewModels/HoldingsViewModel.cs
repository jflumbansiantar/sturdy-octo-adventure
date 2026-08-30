using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PortfolioOS.Mobile.Models;
using PortfolioOS.Mobile.Services;

namespace PortfolioOS.Mobile.ViewModels;

public partial class HoldingsViewModel : ObservableObject
{
    private readonly ApiClient _api;

    public HoldingsViewModel(ApiClient api) => _api = api;

    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private string _successMessage = string.Empty;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private List<HoldingModel> _holdings = [];

    [ObservableProperty] private string _formTicker = string.Empty;
    [ObservableProperty] private string _formName = string.Empty;
    [ObservableProperty] private string _formType = "Stock";
    [ObservableProperty] private string _formSubType = string.Empty;
    [ObservableProperty] private string _formMarket = "US";
    [ObservableProperty] private decimal _formShares;
    [ObservableProperty] private decimal _formAvgCost;

    public string[] Types { get; } = ["Stock", "ETF", "Crypto", "MutualFund"];
    public string[] Markets { get; } = ["US", "ID"];

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(FormTicker) || string.IsNullOrWhiteSpace(FormName))
        {
            ErrorMessage = "Ticker and Name are required.";
            return;
        }

        IsSaving = true;
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;
        try
        {
            await _api.CreateHoldingAsync(new
            {
                ticker = FormTicker.ToUpper().Trim(),
                name = FormName,
                type = FormType,
                subType = FormSubType,
                market = FormMarket,
                shares = FormShares,
                avgCost = FormAvgCost
            });
            SuccessMessage = $"{FormTicker.ToUpper().Trim()} added!";
            await LoadAsync();   // segarkan daftar setelah simpan
            FormTicker = string.Empty;
            FormName = string.Empty;
            FormSubType = string.Empty;
            FormShares = 0;
            FormAvgCost = 0;
        }
        catch
        {
            ErrorMessage = "Failed to save holding.";
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            Holdings = await _api.GetHoldingsAsync();
        }
        catch
        {
            ErrorMessage = "Failed to load holdings.";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
