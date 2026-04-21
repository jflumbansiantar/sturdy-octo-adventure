using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PortfolioOS.Mobile.Services;

namespace PortfolioOS.Mobile.ViewModels;

public partial class TransactionsViewModel : ObservableObject
{
    private readonly ApiClient _api;

    public TransactionsViewModel(ApiClient api) => _api = api;

    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private string _successMessage = string.Empty;
    [ObservableProperty] private bool _isSaving;

    [ObservableProperty] private string _formName = string.Empty;
    [ObservableProperty] private string _formCategory = "Income";
    [ObservableProperty] private string _formType = "Credit";
    [ObservableProperty] private decimal _formTotal;

    public string[] Categories { get; } = ["Income", "Expense", "Stock", "Debt"];

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(FormName))
        {
            ErrorMessage = "Name is required.";
            return;
        }

        IsSaving = true;
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;
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
            SuccessMessage = "Transaction saved!";
            FormName = string.Empty;
            FormTotal = 0;
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
}
