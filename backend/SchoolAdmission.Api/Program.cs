using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Features.Classes;
using SchoolAdmission.Api.Features.Holds;
using SchoolAdmission.Api.Infrastructure;
using SchoolAdmission.Api.Data;
using Serilog;
using Serilog.Formatting.Json;

var builder = WebApplication.CreateBuilder(args);

var port = builder.Configuration["PORT"] ?? builder.Configuration["BACKEND_PORT"];
if (!string.IsNullOrWhiteSpace(port) &&
    string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")) &&
    string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS")))
{
    builder.WebHost.UseUrls($"http://*:{port}");
}

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(new JsonFormatter()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddDbContext<AdmissionDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("Database")
        ?? throw new InvalidOperationException("ConnectionStrings:Database is required.")));
builder.Services.AddScoped<ClassQueries>();
builder.Services.AddHostedService<HoldExpiryWorker>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseCors();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AdmissionDbContext>();
    await db.Database.OpenConnectionAsync();
    try
    {
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_lock(-2)");
        await db.Database.MigrateAsync();
    }
    finally
    {
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock(-2)");
        await db.Database.CloseConnectionAsync();
    }
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapClassEndpoints();
app.MapHoldEndpoints();
app.Run();

public partial class Program;
