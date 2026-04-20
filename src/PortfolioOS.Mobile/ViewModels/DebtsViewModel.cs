using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PortfolioOS.Mobile.Models;
using PortfolioOS.Mobile.Services;

namespace PortfolioOS.Mobile.ViewModels;

public partial class DebtsViewModel : ObservableObject
{
    private readonly ApiClient _api;

    public DebtsViewModel(ApiClient api) => _api = api;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private List<DebtModel> _debts = [];
    [ObservableProperty] private string _errorMessage = string.Empty;

    public decimal TotalOutstanding => Debts.Sum(d => d.Balance);
    public decimal TotalPaid => Debts.Sum(d => d.TotalPaid);
    public decimal MonthlyMinimum => Debts.Where(d => d.Status == "Active").Sum(d => d.MinimumPayment);
    public string TotalOutstandingFormatted => $"${TotalOutstanding:N2}";
    public string MonthlyMinimumFormatted => $"${MonthlyMinimum:N2}";

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            Debts = await _api.GetDebtsAsync();
            OnPropertyChanged(nameof(TotalOutstanding));
            OnPropertyChanged(nameof(TotalPaid));
            OnPropertyChanged(nameof(MonthlyMinimum));
            OnPropertyChanged(nameof(TotalOutstandingFormatted));
            OnPropertyChanged(nameof(MonthlyMinimumFormatted));
        }
        catch
        {
            ErrorMessage = "Failed to load debts.";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
