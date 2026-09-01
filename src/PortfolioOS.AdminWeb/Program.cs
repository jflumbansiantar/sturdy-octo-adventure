using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;
using PortfolioOS.AdminWeb;
using PortfolioOS.AdminWeb.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices(o =>
{
    o.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    o.SnackbarConfiguration.VisibleStateDuration = 4000;
});

var adminApiBase = builder.Configuration["AdminApiBaseUrl"] ?? "https://localhost:7197";

// ---------------------------------------------------------------------------
// Login lewat PortfolioOS.Identity (authorization code + PKCE).
//
// Konsol ini tidak punya halaman login sendiri dan tidak pernah memegang password:
// kredensial hanya diketik di service identity, dan yang kembali ke sini cuma token.
// ---------------------------------------------------------------------------
builder.Services.AddOidcAuthentication(options =>
{
    builder.Configuration.Bind("Oidc", options.ProviderOptions);

    // Scope ditulis di kode, bukan di appsettings: daftar hasil binding akan ditambahkan
    // ke daftar bawaan alih-alih menggantinya, dan scope admin bukan hal yang sebaiknya
    // bisa berubah diam-diam lewat file konfigurasi.
    options.ProviderOptions.DefaultScopes.Clear();
    foreach (var scope in new[]
             {
                 "openid", "profile", "email", "roles",
                 "portfolioos.read", "portfolioos.write", "portfolioos.admin",
             })
    {
        options.ProviderOptions.DefaultScopes.Add(scope);
    }

    options.ProviderOptions.ResponseType = "code";

    // Nama claim mengikuti yang diterbitkan IdentityServer (MapInboundClaims dimatikan
    // di sisi service), jadi <AuthorizeView Roles="admin"> membaca claim yang benar.
    options.UserOptions.NameClaim = "name";
    options.UserOptions.RoleClaim = "role";
})
.AddAccountClaimsPrincipalFactory<ArrayClaimsPrincipalFactory>();

builder.Services.AddTransient<AdminAuthorizationMessageHandler>();

builder.Services
    .AddHttpClient<AdminApiClient>(c => c.BaseAddress = new Uri(adminApiBase.TrimEnd('/') + "/"))
    .AddHttpMessageHandler<AdminAuthorizationMessageHandler>();

await builder.Build().RunAsync();
