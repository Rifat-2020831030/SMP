using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
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
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(404, problem.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("detail").GetString()));
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
    [InlineData("x", 0)]
    [InlineData("Class 6", 0)]
    [InlineData("Class 6", -1)]
    [InlineData("Class 6", -2147483648)]
    public async Task InvalidClass_Returns400(string name, int totalSeats)
    {
        await using var factory = await AdmissionApiFactory.CreateAsync();
        var response = await factory.Client.PostAsJsonAsync("/classes", new { name, totalSeats });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(200, 2147483647)]
    public async Task ValidClassBoundaries_AreAccepted(int nameLength, int totalSeats)
    {
        await using var factory = await AdmissionApiFactory.CreateAsync();
        var name = new string('x', nameLength);
        var response = await factory.Client.PostAsJsonAsync("/classes", new { name, totalSeats });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("{}")]
    [InlineData("{\"name\":null,\"totalSeats\":1}")]
    [InlineData("{\"name\":\"A\",\"totalSeats\":\"1\"}")]
    [InlineData("{\"name\":\"A\",\"totalSeats\":2147483648}")]
    [InlineData("{\"name\":\"A\",\"totalSeats\":1.5}")]
    public async Task InvalidClassPayload_Returns400(string json)
    {
        await using var factory = await AdmissionApiFactory.CreateAsync();
        using var request = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await factory.Client.PostAsync("/classes", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DuplicateTrimmedName_Returns409()
    {
        await using var factory = await AdmissionApiFactory.CreateAsync();
        var first = await factory.Client.PostAsJsonAsync("/classes", new { name = "Class 6", totalSeats = 10 });
        var duplicate = await factory.Client.PostAsJsonAsync("/classes", new { name = " Class 6 ", totalSeats = 12 });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task ConcurrentCaseVariantNames_OneCreatedOneConflict()
    {
        await using var factory = await AdmissionApiFactory.CreateAsync();
        using var start = new ManualResetEventSlim(false);
        var first = Task.Run(async () =>
        {
            start.Wait();
            return await factory.Client.PostAsJsonAsync("/classes", new { name = " Class 6 ", totalSeats = 10 });
        });
        var second = Task.Run(async () =>
        {
            start.Wait();
            return await factory.Client.PostAsJsonAsync("/classes", new { name = "class 6", totalSeats = 10 });
        });
        start.Set();
        var responses = await Task.WhenAll(first, second);
        var statuses = responses.Select(response => (int)response.StatusCode).ToArray();

        Assert.Equal(1, statuses.Count(status => status == 201));
        Assert.Equal(1, statuses.Count(status => status == 409));
        using var list = JsonDocument.Parse(await factory.Client.GetStringAsync("/classes"));
        Assert.Single(list.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task Database_RejectsNegativeAndOverCapacityCounters()
    {
        await using var factory = await AdmissionApiFactory.CreateAsync();
        using var client = factory.Client;
        await client.GetAsync("/classes");
        await using var connection = new NpgsqlConnection(factory.ConnectionString);
        await connection.OpenAsync();
        await using (var insert = new NpgsqlCommand(
            "INSERT INTO classes (id, name, total_seats) VALUES ('db-constraint', 'Database constraint', 2)", connection))
            await insert.ExecuteNonQueryAsync();

        await using var negative = new NpgsqlCommand("UPDATE classes SET held_seats = -1 WHERE id = 'db-constraint'", connection);
        var negativeError = await Assert.ThrowsAsync<PostgresException>(() => negative.ExecuteNonQueryAsync());
        Assert.Equal("ck_classes_nonnegative", negativeError.ConstraintName);

        await using var overflow = new NpgsqlCommand("UPDATE classes SET held_seats = 3 WHERE id = 'db-constraint'", connection);
        var overflowError = await Assert.ThrowsAsync<PostgresException>(() => overflow.ExecuteNonQueryAsync());
        Assert.Equal("ck_classes_capacity", overflowError.ConstraintName);
    }

    [Fact]
    public async Task Database_RejectsDuplicateNormalizedClassName()
    {
        await using var factory = await AdmissionApiFactory.CreateAsync();
        using var client = factory.Client;
        await client.GetAsync("/classes");
        await using var connection = new NpgsqlConnection(factory.ConnectionString);
        await connection.OpenAsync();
        await using (var insert = new NpgsqlCommand(
            "INSERT INTO classes (id, name, total_seats) VALUES ('first', 'Class 6', 2)", connection))
            await insert.ExecuteNonQueryAsync();

        await using var duplicate = new NpgsqlCommand(
            "INSERT INTO classes (id, name, total_seats) VALUES ('second', 'class 6', 2)", connection);
        var error = await Assert.ThrowsAsync<PostgresException>(() => duplicate.ExecuteNonQueryAsync());

        Assert.Equal("uq_class_name", error.ConstraintName);
    }
}
