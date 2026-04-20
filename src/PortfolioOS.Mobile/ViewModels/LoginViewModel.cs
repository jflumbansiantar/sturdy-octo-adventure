using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PortfolioOS.Mobile.Services;

namespace PortfolioOS.Mobile.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly AuthService _auth;

    public LoginViewModel(ApiClient api, AuthService auth)
    {
        _api = api;
        _auth = auth;
    }

    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Username and password are required.";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            var result = await _api.LoginAsync(Username, Password);
            if (result is null)
            {
                ErrorMessage = "Invalid credentials.";
                return;
            }

            _auth.SetToken(result.Token);
            await Shell.Current.GoToAsync("//dashboard");
        }
        catch
        {
            ErrorMessage = "Could not connect to server.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
