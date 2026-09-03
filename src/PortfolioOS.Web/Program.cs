using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using PortfolioOS.Web;
using PortfolioOS.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();

// Kosong = pakai origin situs ini sendiri. Itu yang dipakai deployment di balik reverse
// proxy: web dilayani di "/" dan API di "/api" pada host yang sama, jadi tidak ada nama host
// yang perlu di-hardcode ke dalam file konfigurasi (dan CORS tidak ikut bermain).
var configuredApiBase = builder.Configuration["ApiBaseUrl"];
var apiBase = string.IsNullOrWhiteSpace(configuredApiBase)
    ? builder.HostEnvironment.BaseAddress
    : configuredApiBase;

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AppState>();
builder.Services.AddScoped<UnauthorizedRedirectHandler>();
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<UnauthorizedRedirectHandler>();
    handler.InnerHandler = new HttpClientHandler();
    return new HttpClient(handler) { BaseAddress = new Uri(apiBase) };
});
builder.Services.AddScoped<PortfolioApiClient>();

await builder.Build().RunAsync();
