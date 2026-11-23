using Clubes.Api.Data;
using Clubes.Api.Models;
using Clubes.Api.Security;
using Clubes.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("ibge", client =>
{
    client.BaseAddress = new Uri("https://servicodados.ibge.gov.br/api/v1/localidades/");
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
builder.Services.AddScoped<ClubMembershipService>();
builder.Services.AddScoped<LocalidadesService>();
builder.Services.AddAuthentication("Bearer")
    .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>("Bearer", _ => { });
builder.Services.AddAuthorization();

var databaseProvider = builder.Configuration["Database:Provider"] ?? "Sqlite";
var sqlitePath = builder.Configuration["Database:SqlitePath"] ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "clubes-v2.db");
var mySqlConnection = builder.Configuration.GetConnectionString("MySql");
var databaseConfigured = databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase)
    || (databaseProvider.Equals("MySql", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(mySqlConnection));

if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
{
    var fullPath = Path.GetFullPath(sqlitePath);
    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
    builder.Services.AddDbContext<ClubesDbContext>(options => options.UseSqlite($"Data Source={fullPath}"));
}
else if (databaseProvider.Equals("MySql", StringComparison.OrdinalIgnoreCase))
{
    if (string.IsNullOrWhiteSpace(mySqlConnection))
        throw new InvalidOperationException("Configure ConnectionStrings:MySql para usar MySQL.");
    builder.Services.AddDbContext<ClubesDbContext>(options => options.UseMySQL(mySqlConnection));
}
else
{
    throw new InvalidOperationException("Database:Provider deve ser Sqlite ou MySql.");
}

var app = builder.Build();

if (databaseConfigured)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ClubesDbContext>();
    await db.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/health", (IConfiguration configuration) => Results.Ok(new
{
    status = "online",
    service = "Clubes.Api",
    databaseConfigured
})).AllowAnonymous();

app.Run();

public partial class Program { }
