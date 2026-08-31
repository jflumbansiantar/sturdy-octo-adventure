using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PortfolioOS.Mobile.Converters;
using PortfolioOS.Mobile.Models;
using PortfolioOS.Mobile.Services;

namespace PortfolioOS.Mobile.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly ApiClient _api;

    public DashboardViewModel(ApiClient api) => _api = api;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private decimal _totalValue;
    [ObservableProperty] private decimal _totalGainLoss;
    [ObservableProperty] private decimal _totalGainLossPct;
    [ObservableProperty] private decimal _todayGainLoss;
    [ObservableProperty] private decimal _totalCostBasis;
    [ObservableProperty] private List<HoldingModel> _topMovers = [];
    [ObservableProperty] private List<TransactionModel> _recentTransactions = [];
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private string _baseCurrency = "IDR";
    [ObservableProperty] private decimal _usdIdrRate;
    [ObservableProperty] private bool _isRateLive = true;

    // The API returns these already converted into the portfolio's base currency, so they are
    // formatted with that currency - not the dollar sign this used to hardcode, which labelled
    // a rupiah-dominated total as USD.
    public string TotalValueFormatted => MoneyConverter.Format(TotalValue, BaseCurrency);

    public string TotalGainLossFormatted =>
        $"{Sign(TotalGainLoss)}{MoneyConverter.Format(Math.Abs(TotalGainLoss), BaseCurrency)} ({TotalGainLossPct:F2}%)";

    public string TodayGainLossFormatted =>
        $"{Sign(TodayGainLoss)}{MoneyConverter.Format(Math.Abs(TodayGainLoss), BaseCurrency)}";

    /// <summary>Shown only when the rate is stale, so a total is never quietly approximate.</summary>
    public string RateNotice => IsRateLive
        ? string.Empty
        : $"Kurs tersimpan (1 USD = Rp {UsdIdrRate:N0}) - total bersifat perkiraan.";

    // Format() prints its own minus sign, so the magnitude is passed in and the sign
    // prefixed here - otherwise a loss renders as "-Rp -6.938.722".
    private static string Sign(decimal value) => value >= 0 ? "+" : "-";
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
                BaseCurrency  = summary.BaseCurrency;
                UsdIdrRate    = summary.UsdIdrRate;
                IsRateLive    = summary.IsRateLive;
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
            OnPropertyChanged(nameof(RateNotice));
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
}
