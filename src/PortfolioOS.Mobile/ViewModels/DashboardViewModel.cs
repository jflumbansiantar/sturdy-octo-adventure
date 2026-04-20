using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PortfolioOS.Mobile.Models;
using PortfolioOS.Mobile.Services;

namespace PortfolioOS.Mobile.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly AuthService _auth;

    public DashboardViewModel(ApiClient api, AuthService auth)
    {
        _api = api;
        _auth = auth;
    }

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private decimal _totalValue;
    [ObservableProperty] private decimal _totalGainLoss;
    [ObservableProperty] private decimal _totalGainLossPct;
    [ObservableProperty] private decimal _todayGainLoss;
    [ObservableProperty] private decimal _totalCostBasis;
    [ObservableProperty] private List<HoldingModel> _topMovers = [];
    [ObservableProperty] private List<TransactionModel> _recentTransactions = [];
    [ObservableProperty] private string _errorMessage = string.Empty;

    public string TotalValueFormatted => $"${TotalValue:N2}";
    public string TotalGainLossFormatted => $"{(TotalGainLoss >= 0 ? "+" : "")}${TotalGainLoss:N2} ({TotalGainLossPct:F2}%)";
    public string TodayGainLossFormatted => $"{(TodayGainLoss >= 0 ? "+" : "")}${TodayGainLoss:N2}";
    public Color GainLossColor => TotalGainLoss >= 0 ? Color.FromArgb("#4CAF50") : Color.FromArgb("#F44336");
    public Color TodayGainLossColor => TodayGainLoss >= 0 ? Color.FromArgb("#4CAF50") : Color.FromArgb("#F44336");

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var t1 = _api.GetPortfolioSummaryAsync();
            var t2 = _api.GetTransactionsAsync();
            await Task.WhenAll(t1, t2);

            var summary = t1.Result;
            if (summary is not null)
            {
                TotalValue = summary.TotalValue;
                TotalCostBasis = summary.TotalCostBasis;
                TotalGainLoss = summary.TotalGainLoss;
                TotalGainLossPct = summary.TotalGainLossPct;
                TodayGainLoss = summary.TodayGainLoss;
                TopMovers = summary.Holdings
                    .OrderByDescending(h => Math.Abs(h.DayChangePct))
                    .Take(5)
                    .ToList();
            }

            RecentTransactions = t2.Result.Take(5).ToList();

            OnPropertyChanged(nameof(TotalValueFormatted));
            OnPropertyChanged(nameof(TotalGainLossFormatted));
            OnPropertyChanged(nameof(TodayGainLossFormatted));
            OnPropertyChanged(nameof(GainLossColor));
            OnPropertyChanged(nameof(TodayGainLossColor));
        }
        catch
        {
            ErrorMessage = "Failed to load data.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        _auth.ClearToken();
        await Shell.Current.GoToAsync("//login");
    }
}
