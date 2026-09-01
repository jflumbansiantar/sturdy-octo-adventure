using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PortfolioOS.Admin.Authorization;
using PortfolioOS.Admin.Configuration;
using PortfolioOS.Admin.Data;
using PortfolioOS.Admin.Middleware;
using PortfolioOS.Admin.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Konfigurasi
// ---------------------------------------------------------------------------
var downstream = builder.Configuration.GetSection(DownstreamOptions.SectionName).Get<DownstreamOptions>()
                 ?? new DownstreamOptions();

var connectionString = builder.Configuration.GetConnectionString("AdminConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:AdminConnection belum diisi.");

var authority = builder.Configuration["IdentityServer:Authority"]
    ?? throw new InvalidOperationException("IdentityServer:Authority belum diisi.");

// ---------------------------------------------------------------------------
// Database milik admin service — hanya menyimpan setting web/admin.
// ---------------------------------------------------------------------------
builder.Services.AddDbContext<AdminDbContext>(o => o.UseNpgsql(connectionString));

// ---------------------------------------------------------------------------
// Autentikasi: hanya token dari PortfolioOS.Identity. Token HS256 lama dari
// POST /api/auth/login sengaja tidak diterima di sini — token itu tidak mengenal
// scope maupun role, jadi tidak bisa membuktikan pemakainya seorang admin.
// ---------------------------------------------------------------------------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority = authority;

        // Di dalam container, issuer yang tertulis di token adalah alamat yang dilihat
        // browser, sedangkan metadata harus diambil lewat nama service internal.
        var metadataAddress = builder.Configuration["IdentityServer:MetadataAddress"];
        if (!string.IsNullOrWhiteSpace(metadataAddress)) o.MetadataAddress = metadataAddress;

        o.Audience = builder.Configuration["IdentityServer:Audience"] ?? "portfolioos-api";
        o.RequireHttpsMetadata = builder.Configuration.GetValue(
            "IdentityServer:RequireHttpsMetadata", !builder.Environment.IsDevelopment());

        o.MapInboundClaims = false;
        o.TokenValidationParameters.NameClaimType = "name";
        o.TokenValidationParameters.RoleClaimType = "role";
    });

builder.Services.AddSingleton<IAuthorizationHandler, ScopeRequirementHandler>();

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy(AdminPolicies.AdminOnly, p => p
        .RequireAuthenticatedUser()
        .AddRequirements(new ScopeRequirement(AdminPolicies.Scopes.Admin))
        .RequireRole(AdminPolicies.AdminRole));

    // Tidak ada endpoint anonim selain /health, jadi default-nya pun admin.
    o.DefaultPolicy = o.GetPolicy(AdminPolicies.AdminOnly)!;
    o.FallbackPolicy = o.GetPolicy(AdminPolicies.AdminOnly)!;
});

// ---------------------------------------------------------------------------
// Klien ke service tujuan — token pemanggil diteruskan apa adanya.
// ---------------------------------------------------------------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<BearerForwardingHandler>();

builder.Services
    .AddHttpClient<IdentityAdminClient>(c =>
    {
        c.BaseAddress = new Uri(downstream.IdentityBaseUrl.TrimEnd('/') + "/");
        c.Timeout = TimeSpan.FromSeconds(downstream.TimeoutSeconds);
    })
    .AddHttpMessageHandler<BearerForwardingHandler>()
    .ConfigurePrimaryHttpMessageHandler(() => CreateHandler(builder.Environment, downstream));

builder.Services
    .AddHttpClient<PortfolioApiClient>(c =>
    {
        c.BaseAddress = new Uri(downstream.ApiBaseUrl.TrimEnd('/') + "/");
        c.Timeout = TimeSpan.FromSeconds(downstream.TimeoutSeconds);
    })
    .AddHttpMessageHandler<BearerForwardingHandler>()
    .ConfigurePrimaryHttpMessageHandler(() => CreateHandler(builder.Environment, downstream));

// ---------------------------------------------------------------------------
// CORS — hanya konsol admin yang boleh memanggil service ini dari browser.
// ---------------------------------------------------------------------------
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader()));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "PortfolioOS Admin API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Access token dari PortfolioOS.Identity dengan scope portfolioos.admin dan role admin.",
    });
    c.AddSecurityRequirement(new()
    {
        {
            new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } },
            []
        }
    });
});

builder.Services.AddHealthChecks().AddDbContextCheck<AdminDbContext>();

var app = builder.Build();

// ---------------------------------------------------------------------------
// Migrasi + seed setting web
// ---------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
    await db.Database.MigrateAsync();

    await WebSettingSeeder.SeedAsync(
        db, scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("WebSettingSeeder"));
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

static HttpMessageHandler CreateHandler(IWebHostEnvironment env, DownstreamOptions options)
{
    var handler = new HttpClientHandler();

    // Sertifikat dev ASP.NET Core sering belum di-trust oleh runtime, jadi panggilan
    // https://localhost gagal sebelum sempat mengirim apa pun. Hanya di Development.
    if (env.IsDevelopment() && options.AllowInvalidCertificates)
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

    return handler;
}
