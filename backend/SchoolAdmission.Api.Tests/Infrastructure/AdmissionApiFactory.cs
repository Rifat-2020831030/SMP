using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SchoolAdmission.Api.Tests.Infrastructure;

internal sealed class AdmissionApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string _environmentName;

    public HttpClient Client => CreateClient();
    public string ConnectionString => _connectionString;

    public static async Task<AdmissionApiFactory> CreateAsync(string environmentName = "Development")
    {
        var database = await PostgresFixture.CreateAsync();
        return new AdmissionApiFactory(database.ConnectionString, database, environmentName);
    }

    private readonly PostgresFixture _database;

    private AdmissionApiFactory(string connectionString, PostgresFixture database, string environmentName)
    {
        _connectionString = connectionString;
        _database = database;
        _environmentName = environmentName;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environmentName);
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Database"] = _connectionString }));
        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, TestFailureStartupFilter>());
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }

    private sealed class TestFailureStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.Use(async (context, nextMiddleware) =>
            {
                if (context.Request.Path == "/__test/failure")
                    throw new InvalidOperationException("private diagnostic must not be returned");
                await nextMiddleware();
            });
        };
    }
}
