using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PortfolioOS.Mobile.Services;
using PortfolioOS.Shared.Scanning;

namespace PortfolioOS.Mobile.ViewModels;

/// <summary>
/// The confirmation step between a scan and the database. Nothing the OCR produced is saved
/// until it has been through this screen: every field is editable, and each one carries the
/// scanner's confidence so the user knows which numbers deserve a second look.
/// </summary>
public partial class ScanReviewViewModel : ObservableObject, IQueryAttributable
{
    public const string DraftKey = "draft";

    private readonly ApiClient _api;

    public ScanReviewViewModel(ApiClient api) => _api = api;

    [ObservableProperty] private DateTime _date = DateTime.Today;
    [ObservableProperty] private string _category = "Expense";
    [ObservableProperty] private string _type = "Debit";
    [ObservableProperty] private string _market = "ID";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private decimal _total;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private decimal _shares;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private decimal _price;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isSaving;

    // Drives the colour dot beside each field.
    [ObservableProperty] private Confidence _dateConfidence;
    [ObservableProperty] private Confidence _categoryConfidence;
    [ObservableProperty] private Confidence _nameConfidence;
    [ObservableProperty] private Confidence _totalConfidence;
    [ObservableProperty] private Confidence _sharesConfidence;
    [ObservableProperty] private Confidence _priceConfidence;

    [ObservableProperty] private string _documentLabel = string.Empty;
    [ObservableProperty] private string _rawText = string.Empty;
    [ObservableProperty] private bool _isRawTextVisible;
    [ObservableProperty] private List<string> _warnings = [];
    [ObservableProperty] private string _errorMessage = string.Empty;

    public string[] Categories { get; } = ["Income", "Expense", "Stock", "Debt"];
    public string[] Markets { get; } = ["ID", "US"];

    /// <summary>Market, Shares and Price only apply to - and are only required by - stock trades.</summary>
    public bool IsStock => string.Equals(Category, "Stock", StringComparison.OrdinalIgnoreCase);

    public bool HasWarnings => Warnings.Count > 0;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(DraftKey, out var value) && value is TransactionDraft draft)
            Apply(draft);
    }

    private void Apply(TransactionDraft draft)
    {
        // A missing date is not an error - today is the sensible default, flagged as a guess.
        Date = draft.Date.HasValue ? draft.Date.Value.ToDateTime(TimeOnly.MinValue) : DateTime.Today;
        DateConfidence = draft.Date.Confidence;

        if (draft.Category.HasValue) Category = draft.Category.Value.ToString();
        CategoryConfidence = draft.Category.Confidence;

        Name = draft.Name.Value ?? string.Empty;
        NameConfidence = draft.Name.Confidence;

        if (draft.Type.HasValue) Type = draft.Type.Value!;

        Total = draft.Total.HasValue ? draft.Total.Value : 0m;
        TotalConfidence = draft.Total.Confidence;

        if (draft.Market.HasValue) Market = draft.Market.Value.ToString();
        Shares = draft.Shares.HasValue ? draft.Shares.Value : 0m;
        SharesConfidence = draft.Shares.Confidence;
        Price = draft.Price.HasValue ? draft.Price.Value : 0m;
        PriceConfidence = draft.Price.Confidence;

        DocumentLabel = Describe(draft.Kind);
        RawText = draft.RawText;
        Warnings = draft.Warnings.ToList();

        OnPropertyChanged(nameof(IsStock));
        OnPropertyChanged(nameof(HasWarnings));
    }

    partial void OnCategoryChanged(string value)
    {
        OnPropertyChanged(nameof(IsStock));
        SaveCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Confirms a scanned ticker against the portfolio the user already holds. The holding
    /// record knows its own market, which removes the one field the scanner can only guess at
    /// from the length of the code.
    /// </summary>
    [RelayCommand]
    public async Task MatchHoldingAsync()
    {
        if (!IsStock || string.IsNullOrWhiteSpace(Name)) return;

        try
        {
            var holdings = await _api.GetHoldingsAsync();
            var match = holdings.FirstOrDefault(h =>
                string.Equals(h.Ticker, Name.Trim(), StringComparison.OrdinalIgnoreCase));

            if (match is null) return;

            Name = match.Ticker;
            Market = match.Market;
            NameConfidence = Confidence.High;
        }
        catch
        {
            // Offline or unauthorised: the guessed market stays, and the user can still fix it.
        }
    }

    private bool CanSave()
    {
        if (IsSaving) return false;
        if (string.IsNullOrWhiteSpace(Name)) return false;
        if (Total <= 0) return false;

        // CreateTransactionValidator rejects a stock transaction without all three, so the
        // button stays disabled rather than letting the user hit a 400 from the API.
        return !IsStock || (Shares > 0 && Price > 0 && !string.IsNullOrWhiteSpace(Market));
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        IsSaving = true;
        ErrorMessage = string.Empty;

        try
        {
            await _api.CreateTransactionAsync(new
            {
                date = DateOnly.FromDateTime(Date),
                category = Category,
                name = Name.Trim(),
                type = Type,
                total = Total,
                market = IsStock ? Market : null,
                shares = IsStock ? Shares : (decimal?)null,
                price = IsStock ? Price : (decimal?)null
            });

            // Popping back re-triggers TransactionsPage.OnAppearing, which reloads the list.
            await Shell.Current.GoToAsync("..");
        }
        catch
        {
            ErrorMessage = "Gagal menyimpan transaksi. Periksa koneksi dan isian di atas.";
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void ToggleRawText() => IsRawTextVisible = !IsRawTextVisible;

    private static string Describe(DocumentKind kind) => kind switch
    {
        DocumentKind.Receipt => "Struk belanja",
        DocumentKind.Transfer => "Bukti transfer",
        DocumentKind.Payslip => "Slip gaji",
        DocumentKind.Bill => "Tagihan",
        DocumentKind.BrokerTrade => "Konfirmasi saham",
        _ => "Dokumen tidak dikenali"
    };
}
