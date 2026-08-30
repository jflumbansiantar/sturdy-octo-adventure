using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PortfolioOS.Mobile.Models;
using PortfolioOS.Mobile.Pages;
using PortfolioOS.Mobile.Services;
using PortfolioOS.Mobile.Services.Ocr;
using PortfolioOS.Shared.Scanning;

namespace PortfolioOS.Mobile.ViewModels;

public partial class TransactionsViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly IOcrService _ocr;
    private readonly ReceiptScanner _scanner;

    public TransactionsViewModel(ApiClient api, IOcrService ocr, ReceiptScanner scanner)
    {
        _api = api;
        _ocr = ocr;
        _scanner = scanner;
    }

    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private string _successMessage = string.Empty;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private List<TransactionModel> _transactions = [];

    [ObservableProperty] private string _formName = string.Empty;
    [ObservableProperty] private string _formType = "Credit";
    [ObservableProperty] private decimal _formTotal;
    [ObservableProperty] private DateTime _formDate = DateTime.Today;

    // Stock-only. Without these the API rejects a Stock transaction outright, which is why
    // the category was previously unusable from the phone.
    [ObservableProperty] private string _formMarket = "ID";
    [ObservableProperty] private decimal _formShares;
    [ObservableProperty] private decimal _formPrice;

    [ObservableProperty] private string _formCategory = "Income";

    partial void OnFormCategoryChanged(string value) => OnPropertyChanged(nameof(IsFormStock));

    public bool IsFormStock => string.Equals(FormCategory, "Stock", StringComparison.OrdinalIgnoreCase);

    public string[] Categories { get; } = ["Income", "Expense", "Stock", "Debt"];
    public string[] Markets { get; } = ["ID", "US"];

    /// <summary>
    /// Photographs a document, reads it on the device, and hands the result to the review
    /// screen. Nothing is saved here - the scan only prefills a form the user still confirms.
    /// </summary>
    [RelayCommand]
    private async Task ScanAsync()
    {
        var choice = await Shell.Current.DisplayActionSheet(
            "Scan Dokumen", "Batal", null, "Ambil Foto", "Pilih dari Galeri");

        if (choice is null || choice == "Batal") return;

        string? photoPath = null;
        string? workingCopy = null;

        IsScanning = true;
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;

        try
        {
            var photo = choice == "Ambil Foto"
                ? await CapturePhotoAsync()
                : await MediaPicker.Default.PickPhotoAsync();

            if (photo is null) return;   // user backed out

            photoPath = photo.FullPath;
            workingCopy = await CopyToCacheAsync(photo);

            var ocr = await _ocr.RecognizeAsync(workingCopy);
            var draft = _scanner.Scan(ocr);

            await Shell.Current.GoToAsync(nameof(ScanReviewPage),
                new Dictionary<string, object> { [ScanReviewViewModel.DraftKey] = draft });
        }
        catch (FeatureNotSupportedException)
        {
            ErrorMessage = "Perangkat ini tidak mendukung kamera.";
        }
        catch (PermissionException)
        {
            ErrorMessage = "Izin kamera ditolak. Aktifkan lewat Pengaturan aplikasi.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Gagal membaca gambar: {ex.Message}";
        }
        finally
        {
            IsScanning = false;

            // The photo is deliberately not kept: MediaPicker writes a copy into the app cache
            // and this is the only place that removes it.
            DeleteQuietly(workingCopy);
            DeleteQuietly(photoPath);
        }
    }

    private async Task<FileResult?> CapturePhotoAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<Permissions.Camera>();

        if (status != PermissionStatus.Granted)
        {
            ErrorMessage = "Izin kamera ditolak. Aktifkan lewat Pengaturan aplikasi.";
            return null;
        }

        return await MediaPicker.Default.CapturePhotoAsync();
    }

    /// <summary>Copies the picked image somewhere this code owns, so it can be deleted for certain.</summary>
    private static async Task<string> CopyToCacheAsync(FileResult photo)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, $"scan_{Guid.NewGuid():N}.jpg");

        await using var source = await photo.OpenReadAsync();
        await using var destination = File.Create(path);
        await source.CopyToAsync(destination);

        return path;
    }

    private static void DeleteQuietly(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // A cache file the OS still holds open is not worth failing the scan over.
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(FormName))
        {
            ErrorMessage = "Name is required.";
            return;
        }

        // Checked here rather than server-side so the user gets a specific message instead of
        // a generic failure from CreateTransactionValidator.
        if (IsFormStock && (FormShares <= 0 || FormPrice <= 0))
        {
            ErrorMessage = "Transaksi saham butuh Lembar dan Harga lebih dari 0.";
            return;
        }

        IsSaving = true;
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;
        try
        {
            await _api.CreateTransactionAsync(new
            {
                date = DateOnly.FromDateTime(FormDate),
                category = FormCategory,
                name = FormName,
                type = FormType,
                total = FormTotal,
                market = IsFormStock ? FormMarket : null,
                shares = IsFormStock ? FormShares : (decimal?)null,
                price = IsFormStock ? FormPrice : (decimal?)null
            });
            SuccessMessage = "Transaction saved!";
            await LoadAsync();   // segarkan daftar setelah simpan
            FormName = string.Empty;
            FormTotal = 0;
            FormShares = 0;
            FormPrice = 0;
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
}
