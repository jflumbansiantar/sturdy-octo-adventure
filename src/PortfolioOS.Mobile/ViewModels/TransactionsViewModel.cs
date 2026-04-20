using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PortfolioOS.Mobile.Models;
using PortfolioOS.Mobile.Services;

namespace PortfolioOS.Mobile.ViewModels;

public partial class TransactionsViewModel : ObservableObject
{
    private readonly ApiClient _api;

    public TransactionsViewModel(ApiClient api) => _api = api;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private List<TransactionModel> _transactions = [];
    [ObservableProperty] private string _errorMessage = string.Empty;

    // Add transaction form
    [ObservableProperty] private bool _showAddForm;
    [ObservableProperty] private string _formName = string.Empty;
    [ObservableProperty] private string _formCategory = "Income";
    [ObservableProperty] private string _formType = "Credit";
    [ObservableProperty] private decimal _formTotal;
    [ObservableProperty] private bool _isSaving;

    public string[] Categories { get; } = ["Income", "Expense", "Stock", "Debt"];

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            Transactions = await _api.GetTransactionsAsync();
        }
        catch
        {
            ErrorMessage = "Failed to load transactions.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(FormName))
            return;

        IsSaving = true;
        try
        {
            await _api.CreateTransactionAsync(new
            {
                date = DateOnly.FromDateTime(DateTime.Today),
                category = FormCategory,
                name = FormName,
                type = FormType,
                total = FormTotal
            });
            ShowAddForm = false;
            FormName = string.Empty;
            FormTotal = 0;
            await LoadAsync();
        }
        catch
        {
            ErrorMessage = "Failed to save transaction.";
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void ToggleAddForm() => ShowAddForm = !ShowAddForm;
}
