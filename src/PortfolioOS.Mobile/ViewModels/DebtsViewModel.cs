using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PortfolioOS.Mobile.Models;
using PortfolioOS.Mobile.Services;

namespace PortfolioOS.Mobile.ViewModels;

public partial class DebtsViewModel : ObservableObject
{
    private readonly ApiClient _api;

    public DebtsViewModel(ApiClient api) => _api = api;

    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private string _successMessage = string.Empty;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private List<DebtModel> _debts = [];

    [ObservableProperty] private string _formName = string.Empty;
    [ObservableProperty] private string _formType = "CreditCard";
    [ObservableProperty] private decimal _formBalance;
    [ObservableProperty] private decimal _formInterestRate;
    [ObservableProperty] private decimal _formMinimumPayment;
    [ObservableProperty] private int _formDueDay = 1;
    [ObservableProperty] private string _formCurrency = "USD";
    [ObservableProperty] private string _formDebtApp = string.Empty;
    [ObservableProperty] private string _formNotes = string.Empty;

    public string[] Types { get; } = ["CreditCard", "PersonalLoan", "Mortgage", "AutoLoan", "StudentLoan", "Other"];
    public string[] Currencies { get; } = ["USD", "IDR"];

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
            await _api.CreateDebtAsync(new
            {
                name = FormName,
                type = FormType,
                balance = FormBalance,
                interestRate = FormInterestRate,
                minimumPayment = FormMinimumPayment,
                dueDay = FormDueDay,
                currency = FormCurrency,
                debtApp = FormDebtApp,
                notes = FormNotes
            });
            SuccessMessage = $"{FormName} added!";
            await LoadAsync();   // segarkan daftar setelah simpan
            FormName = string.Empty;
            FormBalance = 0;
            FormInterestRate = 0;
            FormMinimumPayment = 0;
            FormDueDay = 1;
            FormDebtApp = string.Empty;
            FormNotes = string.Empty;
        }
        catch
        {
            ErrorMessage = "Failed to save debt.";
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
            Debts = await _api.GetDebtsAsync();
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
