using System.Net.Http.Headers;
using System.Net.Http.Json;
using PortfolioOS.Mobile.Models;

namespace PortfolioOS.Mobile.Services;

public class ApiClient
{
    private readonly HttpClient _http;
    private readonly AuthService _auth;

    public ApiClient(HttpClient http, AuthService auth)
    {
        _http = http;
        _auth = auth;
    }

    private void Prepare()
    {
        var token = _auth.GetToken();
        _http.DefaultRequestHeaders.Authorization =
            token is null ? null : new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<LoginResponse?> LoginAsync(string username, string password)
    {
        var resp = await _http.PostAsJsonAsync("api/auth/login", new { username, password });
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<LoginResponse>();
    }

    public async Task<PortfolioSummaryModel?> GetPortfolioSummaryAsync()
    {
        Prepare();
        return await _http.GetFromJsonAsync<PortfolioSummaryModel>("api/portfolio/summary");
    }

    public async Task<List<HoldingModel>> GetHoldingsAsync()
    {
        Prepare();
        return await _http.GetFromJsonAsync<List<HoldingModel>>("api/holdings") ?? [];
    }

    public async Task<List<TransactionModel>> GetTransactionsAsync(string? category = null)
    {
        Prepare();
        var url = "api/transactions" + (category is not null ? $"?category={category}" : "");
        return await _http.GetFromJsonAsync<List<TransactionModel>>(url) ?? [];
    }

    public async Task CreateTransactionAsync(object body)
    {
        Prepare();
        var resp = await _http.PostAsJsonAsync("api/transactions", body);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<List<DebtModel>> GetDebtsAsync()
    {
        Prepare();
        return await _http.GetFromJsonAsync<List<DebtModel>>("api/debts") ?? [];
    }
}
