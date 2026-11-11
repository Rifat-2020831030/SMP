using System.Net;
using System.Text.Json;
using SchoolAdmission.Api.Tests.Infrastructure;

namespace SchoolAdmission.Api.Tests.InfrastructureTests;

public sealed class ErrorContractTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task UnexpectedExceptions_ReturnGenericProblemDetails(string environment)
    {
        await using var factory = await AdmissionApiFactory.CreateAsync(environment);
        var response = await factory.Client.GetAsync("/__test/failure");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = problem.RootElement;
        Assert.Equal(500, root.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("type").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("title").GetString()));
        var detail = root.GetProperty("detail").GetString();
        Assert.False(string.IsNullOrWhiteSpace(detail));
        Assert.DoesNotContain("private diagnostic", detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InvalidOperationException", await response.Content.ReadAsStringAsync());
    }
}
