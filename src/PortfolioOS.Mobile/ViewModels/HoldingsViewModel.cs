using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PortfolioOS.Mobile.Models;
using PortfolioOS.Mobile.Services;

namespace PortfolioOS.Mobile.ViewModels;

public partial class HoldingsViewModel : ObservableObject
{
    private readonly ApiClient _api;

    public HoldingsViewModel(ApiClient api) => _api = api;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private List<HoldingModel> _holdings = [];
    [ObservableProperty] private string _errorMessage = string.Empty;

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
