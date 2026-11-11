using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SchoolAdmission.Api.Tests.Infrastructure;

namespace SchoolAdmission.Api.Tests.Features;

public sealed class ClassesFlowTests
{
    [Fact]
    public async Task CreateClass_PersistsAndLists()
    {
        await using var factory = await AdmissionApiFactory.CreateAsync();
        var response = await factory.Client.PostAsJsonAsync("/classes", new { name = " Class 6 ", totalSeats = 10 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var created = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = created.RootElement.GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(id));
        Assert.Equal("Class 6", created.RootElement.GetProperty("name").GetString());
        Assert.Equal(10, created.RootElement.GetProperty("totalSeats").GetInt32());

        using var list = JsonDocument.Parse(await factory.Client.GetStringAsync("/classes"));
        Assert.Equal(JsonValueKind.Array, list.RootElement.ValueKind);
        var summary = Assert.Single(list.RootElement.EnumerateArray().ToArray());
        Assert.Equal(id, summary.GetProperty("id").GetString());
        Assert.Equal(0, summary.GetProperty("heldSeats").GetInt32());
        Assert.Equal(0, summary.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(10, summary.GetProperty("availableSeats").GetInt32());

        using var detail = JsonDocument.Parse(await factory.Client.GetStringAsync($"/classes/{id}"));
        Assert.Equal("Class 6", detail.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task UnknownClass_Returns404()
    {
        await using var factory = await AdmissionApiFactory.CreateAsync();
        var response = await factory.Client.GetAsync("/classes/unknown-class");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EmptyList_IsArray()
    {
        await using var factory = await AdmissionApiFactory.CreateAsync();
        using var list = JsonDocument.Parse(await factory.Client.GetStringAsync("/classes"));

        Assert.Equal(JsonValueKind.Array, list.RootElement.ValueKind);
        Assert.Empty(list.RootElement.EnumerateArray());
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("   ", 1)]
    [InlineData("Class 6", 0)]
    [InlineData("Class 6", -1)]
    public async Task InvalidClass_Returns400(string name, int totalSeats)
    {
        await using var factory = await AdmissionApiFactory.CreateAsync();
        var response = await factory.Client.PostAsJsonAsync("/classes", new { name, totalSeats });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
