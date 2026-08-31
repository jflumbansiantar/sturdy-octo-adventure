using Microsoft.EntityFrameworkCore;
using PortfolioOS.API.Authorization;
using PortfolioOS.API.Middleware;
using PortfolioOS.Application;
using PortfolioOS.Infrastructure;
using PortfolioOS.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Application + Infrastructure layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Controllers
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// Swagger
var identityAuthority = builder.Configuration["IdentityServer:Authority"];

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "PortfolioOS API", Version = "v1" });

    // Tombol "Authorize" tetap menerima token yang ditempel manual (jalur lama).
    c.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
    });
    c.AddSecurityRequirement(new()
    {
        {
            new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } },
            []
        }
    });

    if (!string.IsNullOrWhiteSpace(identityAuthority))
    {
        var authority = identityAuthority.TrimEnd('/');

        c.AddSecurityDefinition("oauth2", new()
        {
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.OAuth2,
            Flows = new()
            {
                AuthorizationCode = new()
                {
                    AuthorizationUrl = new Uri($"{authority}/connect/authorize"),
                    TokenUrl = new Uri($"{authority}/connect/token"),
                    Scopes = new Dictionary<string, string>
                    {
                        ["openid"] = "Identitas dasar",
                        ["profile"] = "Profil pengguna",
                        ["roles"] = "Role pengguna",
                        [PortfolioPolicies.Scopes.Read] = "Baca data portofolio",
                        [PortfolioPolicies.Scopes.Write] = "Ubah data portofolio",
                        [PortfolioPolicies.Scopes.Admin] = "Administrasi",
                    },
                },
            },
        });
        c.AddSecurityRequirement(new()
        {
            {
                new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "oauth2" } },
                ["openid", "profile", "roles", PortfolioPolicies.Scopes.Read, PortfolioPolicies.Scopes.Write]
            }
        });
    }
});

// Autentikasi: token PortfolioOS.Identity (OIDC) dan/atau token HS256 lama.
// Policy berbasis scope didaftarkan sekalian di sini.
builder.Services.AddPortfolioAuthentication(builder.Configuration, builder.Environment);

// CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader()));

// HttpClient for Yahoo Finance (registered in Infrastructure)
builder.Services.AddHttpClient();

var app = builder.Build();

// Apply pending migrations and seed initial data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await DataSeeder.SeedAsync(db);
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        if (string.IsNullOrWhiteSpace(identityAuthority)) return;

        c.OAuthClientId("portfolioos-swagger");
        c.OAuthAppName("PortfolioOS API - Swagger UI");
        c.OAuthUsePkce();
    });
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
