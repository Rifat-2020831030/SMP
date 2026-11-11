using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace SchoolAdmission.Api.Tests.Infrastructure;

internal sealed class AdmissionApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public HttpClient Client => CreateClient();
    public string ConnectionString => _connectionString;

    public static async Task<AdmissionApiFactory> CreateAsync()
    {
        var database = await PostgresFixture.CreateAsync();
        return new AdmissionApiFactory(database.ConnectionString, database);
    }

    private readonly PostgresFixture _database;

    private AdmissionApiFactory(string connectionString, PostgresFixture database)
    {
        _connectionString = connectionString;
        _database = database;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Database"] = _connectionString }));
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }
}
