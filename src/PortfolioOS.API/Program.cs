using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PortfolioOS.API.Middleware;
using PortfolioOS.API.Services;
using PortfolioOS.Application;
using PortfolioOS.Infrastructure;
using PortfolioOS.Infrastructure.Demo;
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
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "PortfolioOS API", Version = "v1" });
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
});

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization();

// CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader()));

// HttpClient for Yahoo Finance (registered in Infrastructure)
builder.Services.AddHttpClient();

// Keeps the chat search index in step with the data. Degrades quietly when the embedding
// model has not been downloaded - see ChatIndexBackgroundService.
builder.Services.AddHostedService<ChatIndexBackgroundService>();

// Reclaims the schemas of demo sessions nobody logged out of.
builder.Services.AddHostedService<DemoSessionJanitor>();

var app = builder.Build();

// Apply pending migrations and seed initial data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await DataSeeder.SeedAsync(db);
}

// Creates the demo registry and clears sandboxes left over from a previous run.
await app.Services.GetRequiredService<DemoSessionManager>().InitializeAsync();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();

// Between the two on purpose: it reads the token's claims, and everything downstream - every
// controller, every DbContext - depends on the schema it binds.
app.UseMiddleware<DemoSessionMiddleware>();

app.UseAuthorization();
app.MapControllers();

app.Run();
