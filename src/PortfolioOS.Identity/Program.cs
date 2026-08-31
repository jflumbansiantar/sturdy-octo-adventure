using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PortfolioOS.Identity.Config;
using PortfolioOS.Identity.Data;
using PortfolioOS.Identity.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// ---------------------------------------------------------------------------
// Konfigurasi
// ---------------------------------------------------------------------------
var clientUrls = builder.Configuration.GetSection(ClientUrlOptions.SectionName).Get<ClientUrlOptions>()
                 ?? new ClientUrlOptions();
var seedUsers = builder.Configuration.GetSection(SeedUserOptions.SectionName).Get<SeedUserOptions[]>() ?? [];

var connectionString = builder.Configuration.GetConnectionString("IdentityConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:IdentityConnection belum diisi.");

var migrationsAssembly = typeof(Program).Assembly.GetName().Name;

// ---------------------------------------------------------------------------
// ASP.NET Core Identity — store user, role, dan password hash
// ---------------------------------------------------------------------------
builder.Services.AddDbContext<PortfolioIdentityDbContext>(o =>
    o.UseNpgsql(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly)));

builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(o =>
    {
        o.Password.RequiredLength = 10;
        o.Password.RequireDigit = true;
        o.Password.RequireUppercase = true;
        o.Password.RequireNonAlphanumeric = true;

        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        o.Lockout.AllowedForNewUsers = true;

        o.User.RequireUniqueEmail = true;
        o.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<PortfolioIdentityDbContext>()
    .AddDefaultTokenProviders();

// SameSite=None (wajib untuk silent-renew SPA lewat iframe) hanya bisa dipakai di atas
// HTTPS, sedangkan compose lokal berjalan di HTTP. Karena itu dibuat konfigurabel.
var cookieSameSite = Enum.TryParse<SameSiteMode>(
    builder.Configuration["Cookie:SameSite"], ignoreCase: true, out var parsedSameSite)
    ? parsedSameSite
    : SameSiteMode.Lax;

builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "portfolioos.identity";
    o.Cookie.SameSite = cookieSameSite;
    o.Cookie.SecurePolicy = cookieSameSite == SameSiteMode.None
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.SlidingExpiration = true;

    // Cookie auth default me-redirect ke /Account/Login yang mengembalikan HTML.
    // Untuk endpoint /api/* jawaban yang benar adalah 401/403 apa adanya.
    o.LoginPath = "/Account/Login";
    o.LogoutPath = "/Account/Logout";
    o.AccessDeniedPath = "/Account/AccessDenied";
});

// ---------------------------------------------------------------------------
// Duende IdentityServer
// ---------------------------------------------------------------------------
var identityServer = builder.Services
    .AddIdentityServer(o =>
    {
        o.Events.RaiseErrorEvents = true;
        o.Events.RaiseInformationEvents = true;
        o.Events.RaiseFailureEvents = true;
        o.Events.RaiseSuccessEvents = true;

        o.UserInteraction.LoginUrl = "/Account/Login";
        o.UserInteraction.LogoutUrl = "/Account/Logout";
        o.UserInteraction.ErrorUrl = "/Error";

        // Menyertakan audience statis (issuer + "/resources") di access token.
        o.EmitStaticAudienceClaim = true;

        // Signing key dikelola eksplisit di bawah (developer key / sertifikat),
        // bukan lewat automatic key management.
        o.KeyManagement.Enabled = false;

        var licenseKey = builder.Configuration["IdentityServer:LicenseKey"];
        if (!string.IsNullOrWhiteSpace(licenseKey)) o.LicenseKey = licenseKey;

        var issuerUri = builder.Configuration["IdentityServer:IssuerUri"];
        if (!string.IsNullOrWhiteSpace(issuerUri)) o.IssuerUri = issuerUri;
    })
    .AddInMemoryIdentityResources(IdentityServerConfig.IdentityResources)
    .AddInMemoryApiScopes(IdentityServerConfig.ApiScopes)
    .AddInMemoryApiResources(IdentityServerConfig.ApiResources)
    .AddInMemoryClients(IdentityServerConfig.Clients(clientUrls))

    // Persisted grant: refresh token, authorization code, consent, device code.
    // Wajib disimpan di database supaya token tetap valid saat service di-restart
    // atau dijalankan lebih dari satu instance.
    .AddOperationalStore(o =>
    {
        o.ConfigureDbContext = b =>
            b.UseNpgsql(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly));
        o.EnableTokenCleanup = true;
        o.RemoveConsumedTokens = true;
        o.TokenCleanupInterval = 3600; // detik
    })
    .AddAspNetIdentity<ApplicationUser>()
    .AddProfileService<PortfolioProfileService>();

if (builder.Environment.IsDevelopment())
{
    // Membuat tempkey.jwk di folder aplikasi. Jangan dipakai di produksi.
    identityServer.AddDeveloperSigningCredential();
}
else
{
    // appsettings.json memuat kunci ini sebagai string kosong, jadi pengecekannya
    // harus IsNullOrWhiteSpace — "??" hanya menangkap null dan membiarkan path kosong
    // lolos sampai X509Certificate2 melempar CryptographicException yang menyesatkan.
    var certPath = builder.Configuration["IdentityServer:SigningCertificate:Path"];
    if (string.IsNullOrWhiteSpace(certPath))
        throw new InvalidOperationException(
            "IdentityServer:SigningCertificate:Path wajib diisi di luar Development.");

    if (!File.Exists(certPath))
        throw new InvalidOperationException(
            $"Sertifikat penandatangan tidak ditemukan di '{certPath}'.");

    var certPassword = builder.Configuration["IdentityServer:SigningCertificate:Password"];

    identityServer.AddSigningCredential(
        new X509Certificate2(certPath, certPassword, X509KeyStorageFlags.EphemeralKeySet));
}

// ---------------------------------------------------------------------------
// Autentikasi bearer untuk admin API milik service ini sendiri (/api/users).
// Scheme cookie tetap jadi default — dipakai UI login.
// ---------------------------------------------------------------------------
builder.Services.AddAuthentication()
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, o =>
    {
        o.Authority = builder.Configuration["IdentityServer:PublicOrigin"]
                      ?? builder.Configuration["IdentityServer:IssuerUri"];

        // Di dalam container, alamat publik issuer belum tentu bisa di-resolve oleh
        // service itu sendiri; MetadataAddress memisahkan "alamat untuk ambil metadata"
        // dari "issuer yang tertulis di token".
        var metadataAddress = builder.Configuration["IdentityServer:MetadataAddress"];
        if (!string.IsNullOrWhiteSpace(metadataAddress)) o.MetadataAddress = metadataAddress;

        o.Audience = IdentityServerConfig.ApiResourceName;
        o.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        o.MapInboundClaims = false;
        o.TokenValidationParameters.NameClaimType = "name";
        o.TokenValidationParameters.RoleClaimType = "role";
    });

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy(AuthorizationPolicies.AdminApi, p => p
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .RequireClaim("scope", IdentityServerConfig.Scopes.Admin)
        .RequireRole(Roles.Admin));
});

builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddHealthChecks().AddDbContextCheck<PortfolioIdentityDbContext>();

var app = builder.Build();

// ---------------------------------------------------------------------------
// Migrasi + seed
// ---------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;

    await sp.GetRequiredService<PersistedGrantDbContext>().Database.MigrateAsync();

    var identityDb = sp.GetRequiredService<PortfolioIdentityDbContext>();
    await identityDb.Database.MigrateAsync();

    await IdentitySeeder.SeedAsync(
        sp.GetRequiredService<RoleManager<ApplicationRole>>(),
        sp.GetRequiredService<UserManager<ApplicationUser>>(),
        seedUsers,
        sp.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder"));
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseSerilogRequestLogging();
app.UseStaticFiles();
app.UseRouting();

// UseIdentityServer sudah memasang UseAuthentication di dalamnya.
app.UseIdentityServer();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
