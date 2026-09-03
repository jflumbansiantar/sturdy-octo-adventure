using System.Net.Http.Headers;
using System.Net.Http.Json;
using PortfolioOS.Web.Models;

namespace PortfolioOS.Web.Services;

public class PortfolioApiClient(HttpClient http, AuthService auth)
{
    private async Task PrepareAsync()
    {
        var token = await auth.GetTokenAsync();
        http.DefaultRequestHeaders.Authorization =
            token is null ? null : new AuthenticationHeaderValue("Bearer", token);
    }

    // Auth
    public async Task<LoginOutcome> LoginAsync(string username, string password)
    {
        var resp = await http.PostAsJsonAsync("api/auth/login", new { username, password });
        if (resp.IsSuccessStatusCode)
            return new LoginOutcome(await resp.Content.ReadFromJsonAsync<LoginResponse>(), null);

        return new LoginOutcome(null, await ReadErrorAsync(resp));
    }

    /// <summary>The fixed test-drive credentials, or null when the API has demo mode switched off.</summary>
    public async Task<DemoInfoModel?> GetDemoInfoAsync()
    {
        try
        {
            return await http.GetFromJsonAsync<DemoInfoModel>("api/auth/demo");
        }
        catch
        {
            // An older API, or one that is simply down. The login page hides the demo card and
            // still lets the owner sign in normally.
            return null;
        }
    }

    /// <summary>
    /// Ends the session server-side. For the demo account this is what deletes its data.
    /// </summary>
    /// <returns>True when the server confirmed it deleted the session's data.</returns>
    public async Task<bool> LogoutAsync()
    {
        await PrepareAsync();
        try
        {
            var resp = await http.PostAsync("api/auth/logout", null);
            if (!resp.IsSuccessStatusCode) return false;

            var result = await resp.Content.ReadFromJsonAsync<LogoutResult>();
            return result?.DataDeleted ?? false;
        }
        catch
        {
            // The token is cleared by the caller either way. An unreachable API only means the
            // sandbox waits for the server's idle timeout instead of going now.
            return false;
        }
    }

    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            return string.IsNullOrWhiteSpace(body?.Error) ? null : body.Error;
        }
        catch
        {
            return null;
        }
    }

    private record ErrorResponse(string? Error);

    private record LogoutResult(bool DataDeleted);

    // Holdings
    public async Task<List<HoldingModel>> GetHoldingsAsync()
    {
        await PrepareAsync();
        return await http.GetFromJsonAsync<List<HoldingModel>>("api/holdings") ?? [];
    }

    public async Task<Guid> CreateHoldingAsync(object body)
    {
        await PrepareAsync();
        var resp = await http.PostAsJsonAsync("api/holdings", body);
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync<IdResponse>();
        return result!.Id;
    }

    public async Task UpdateHoldingAsync(Guid id, object body)
    {
        await PrepareAsync();
        var resp = await http.PatchAsJsonAsync($"api/holdings/{id}", body);
        resp.EnsureSuccessStatusCode();
    }

    public async Task DeleteHoldingAsync(Guid id)
    {
        await PrepareAsync();
        var resp = await http.DeleteAsync($"api/holdings/{id}");
        resp.EnsureSuccessStatusCode();
    }

    // Portfolio
    public async Task<PortfolioSummaryModel?> GetPortfolioSummaryAsync()
    {
        await PrepareAsync();
        return await http.GetFromJsonAsync<PortfolioSummaryModel>("api/portfolio/summary");
    }

    /// <summary>
    /// USD-IDR rate for the currency toggle. Returns null rather than throwing: a missing
    /// rate should grey out the dollar view, not break the page.
    /// </summary>
    public async Task<ExchangeRateModel?> GetExchangeRateAsync()
    {
        await PrepareAsync();
        try
        {
            return await http.GetFromJsonAsync<ExchangeRateModel>("api/market/fx");
        }
        catch
        {
            return null;
        }
    }

    // Transactions
    public async Task<List<TransactionModel>> GetTransactionsAsync(string? category = null, DateOnly? from = null, DateOnly? to = null)
    {
        await PrepareAsync();
        var qs = new List<string>();
        if (category is not null) qs.Add($"category={category}");
        if (from.HasValue) qs.Add($"from={from.Value:yyyy-MM-dd}");
        if (to.HasValue) qs.Add($"to={to.Value:yyyy-MM-dd}");
        var url = "api/transactions" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");
        return await http.GetFromJsonAsync<List<TransactionModel>>(url) ?? [];
    }

    public async Task CreateTransactionAsync(object body)
    {
        await PrepareAsync();
        var resp = await http.PostAsJsonAsync("api/transactions", body);
        resp.EnsureSuccessStatusCode();
    }

    public async Task DeleteTransactionAsync(Guid id)
    {
        await PrepareAsync();
        var resp = await http.DeleteAsync($"api/transactions/{id}");
        resp.EnsureSuccessStatusCode();
    }

    // Ledger - Accounts
    public async Task<List<LedgerAccountModel>> GetAccountsAsync()
    {
        await PrepareAsync();
        return await http.GetFromJsonAsync<List<LedgerAccountModel>>("api/ledger/accounts") ?? [];
    }

    public async Task CreateAccountAsync(object body)
    {
        await PrepareAsync();
        var resp = await http.PostAsJsonAsync("api/ledger/accounts", body);
        resp.EnsureSuccessStatusCode();
    }

    public async Task UpdateAccountAsync(string id, object body)
    {
        await PrepareAsync();
        var resp = await http.PatchAsJsonAsync($"api/ledger/accounts/{id}", body);
        resp.EnsureSuccessStatusCode();
    }

    // Ledger - Entries
    public async Task<List<JournalEntryModel>> GetEntriesAsync(DateOnly? from = null, DateOnly? to = null)
    {
        await PrepareAsync();
        var qs = new List<string>();
        if (from.HasValue) qs.Add($"from={from.Value:yyyy-MM-dd}");
        if (to.HasValue) qs.Add($"to={to.Value:yyyy-MM-dd}");
        var url = "api/ledger/entries" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");
        return await http.GetFromJsonAsync<List<JournalEntryModel>>(url) ?? [];
    }

    public async Task CreateJournalEntryAsync(object body)
    {
        await PrepareAsync();
        var resp = await http.PostAsJsonAsync("api/ledger/entries", body);
        resp.EnsureSuccessStatusCode();
    }

    public async Task DeleteJournalEntryAsync(string id)
    {
        await PrepareAsync();
        var resp = await http.DeleteAsync($"api/ledger/entries/{id}");
        resp.EnsureSuccessStatusCode();
    }

    public async Task<LedgerSummaryModel?> GetLedgerSummaryAsync()
    {
        await PrepareAsync();
        return await http.GetFromJsonAsync<LedgerSummaryModel>("api/ledger/summary");
    }

    // Debts
    public async Task<List<DebtModel>> GetDebtsAsync()
    {
        await PrepareAsync();
        return await http.GetFromJsonAsync<List<DebtModel>>("api/debts") ?? [];
    }

    public async Task<Guid> CreateDebtAsync(object body)
    {
        await PrepareAsync();
        var resp = await http.PostAsJsonAsync("api/debts", body);
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync<IdResponse>();
        return result!.Id;
    }

    public async Task UpdateDebtAsync(Guid id, object body)
    {
        await PrepareAsync();
        var resp = await http.PatchAsJsonAsync($"api/debts/{id}", body);
        resp.EnsureSuccessStatusCode();
    }

    public async Task DeleteDebtAsync(Guid id)
    {
        await PrepareAsync();
        var resp = await http.DeleteAsync($"api/debts/{id}");
        resp.EnsureSuccessStatusCode();
    }

    // Market
    public async Task<List<QuoteModel>> GetQuotesAsync()
    {
        await PrepareAsync();
        return await http.GetFromJsonAsync<List<QuoteModel>>("api/market/quotes") ?? [];
    }

    // Settings
    public async Task<List<SettingModel>> GetSettingsAsync()
    {
        await PrepareAsync();
        return await http.GetFromJsonAsync<List<SettingModel>>("api/settings") ?? [];
    }

    public async Task UpdateSettingAsync(string key, string value)
    {
        await PrepareAsync();
        var resp = await http.PatchAsJsonAsync("api/settings", new { key, value });
        resp.EnsureSuccessStatusCode();
    }

    // Chat
    public async Task<ChatAnswerModel?> AskChatAsync(string question)
    {
        await PrepareAsync();
        var resp = await http.PostAsJsonAsync("api/chat", new { question });
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ChatAnswerModel>();
    }

    private record IdResponse(Guid Id);
}
