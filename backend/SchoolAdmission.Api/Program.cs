using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;
using Serilog;
using Serilog.Formatting.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(new JsonFormatter()));

builder.Services.AddProblemDetails();
builder.Services.AddDbContext<AdmissionDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("Database")
        ?? throw new InvalidOperationException("ConnectionStrings:Database is required.")));

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseCors();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.Run();

public partial class Program;
