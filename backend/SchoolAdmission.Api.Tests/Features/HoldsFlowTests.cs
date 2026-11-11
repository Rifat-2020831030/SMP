using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SchoolAdmission.Api.Tests.Infrastructure;
using Xunit;

namespace SchoolAdmission.Api.Tests.Features;

public sealed class HoldsFlowTests
{
    private static HttpRequestMessage HoldRequest(HttpMethod method, string path, string studentId)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Add("X-Student-Id", studentId);
        return req;
    }

    [Fact]
    public async Task ApplyForSeat_CreatesHoldAndDecrementsAvailability()
    {
        await using var factory = await AdmissionApiFactory.CreateAsync();
        var createResponse = await factory.Client.PostAsJsonAsync("/classes", new { name = "Class 10", totalSeats = 10 });
        createResponse.EnsureSuccessStatusCode();
        using var createDoc = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var classId = createDoc.RootElement.GetProperty("id").GetString()!;

        var studentId = "12345678901234567";
        using var holdRequest = HoldRequest(HttpMethod.Post, $"/classes/{classId}/holds", studentId);
        var holdResponse = await factory.Client.SendAsync(holdRequest);

        Assert.Equal(HttpStatusCode.Created, holdResponse.StatusCode);
        using var holdDoc = JsonDocument.Parse(await holdResponse.Content.ReadAsStringAsync());

        var holdId = holdDoc.RootElement.TryGetProperty("holdId", out var hidProp)
            ? hidProp.GetString()
            : holdDoc.RootElement.GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(holdId));
        Assert.Equal(studentId, holdDoc.RootElement.GetProperty("studentId").GetString());
        Assert.Equal("HELD", holdDoc.RootElement.GetProperty("status").GetString());

        var expiresAtStr = holdDoc.RootElement.GetProperty("expiresAt").GetString();
        Assert.NotNull(expiresAtStr);
        var expiresAt = DateTimeOffset.Parse(expiresAtStr);
        Assert.True(expiresAt > DateTimeOffset.UtcNow);

        using var classDoc = JsonDocument.Parse(await factory.Client.GetStringAsync($"/classes/{classId}"));
        Assert.Equal(9, classDoc.RootElement.GetProperty("availableSeats").GetInt32());
        Assert.Equal(1, classDoc.RootElement.GetProperty("heldSeats").GetInt32());
    }

    [Fact]
    public async Task ConfirmHold_TransitionsToConfirmed()
    {
        await using var factory = await AdmissionApiFactory.CreateAsync();
        var createResponse = await factory.Client.PostAsJsonAsync("/classes", new { name = "Class 10", totalSeats = 10 });
        createResponse.EnsureSuccessStatusCode();
        using var createDoc = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var classId = createDoc.RootElement.GetProperty("id").GetString()!;

        var studentId = "12345678901234567";
        using var holdRequest = HoldRequest(HttpMethod.Post, $"/classes/{classId}/holds", studentId);
        var holdResponse = await factory.Client.SendAsync(holdRequest);
        Assert.Equal(HttpStatusCode.Created, holdResponse.StatusCode);
        using var holdDoc = JsonDocument.Parse(await holdResponse.Content.ReadAsStringAsync());
        var holdId = holdDoc.RootElement.TryGetProperty("holdId", out var hidProp)
            ? hidProp.GetString()!
            : holdDoc.RootElement.GetProperty("id").GetString()!;

        using var confirmRequest = HoldRequest(HttpMethod.Post, $"/holds/{holdId}/confirm", studentId);
        var confirmResponse = await factory.Client.SendAsync(confirmRequest);
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        using var confirmDoc = JsonDocument.Parse(await confirmResponse.Content.ReadAsStringAsync());
        Assert.Equal("CONFIRMED", confirmDoc.RootElement.GetProperty("status").GetString());

        using var classDoc = JsonDocument.Parse(await factory.Client.GetStringAsync($"/classes/{classId}"));
        Assert.Equal(1, classDoc.RootElement.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(0, classDoc.RootElement.GetProperty("heldSeats").GetInt32());
        Assert.Equal(9, classDoc.RootElement.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task CancelHold_ReleasesSeats()
    {
        await using var factory = await AdmissionApiFactory.CreateAsync();
        var createResponse = await factory.Client.PostAsJsonAsync("/classes", new { name = "Class 10", totalSeats = 10 });
        createResponse.EnsureSuccessStatusCode();
        using var createDoc = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var classId = createDoc.RootElement.GetProperty("id").GetString()!;

        var studentId = "12345678901234567";
        using var holdRequest = HoldRequest(HttpMethod.Post, $"/classes/{classId}/holds", studentId);
        var holdResponse = await factory.Client.SendAsync(holdRequest);
        Assert.Equal(HttpStatusCode.Created, holdResponse.StatusCode);
        using var holdDoc = JsonDocument.Parse(await holdResponse.Content.ReadAsStringAsync());
        var holdId = holdDoc.RootElement.TryGetProperty("holdId", out var hidProp)
            ? hidProp.GetString()!
            : holdDoc.RootElement.GetProperty("id").GetString()!;

        using var cancelRequest = HoldRequest(HttpMethod.Delete, $"/holds/{holdId}", studentId);
        var cancelResponse = await factory.Client.SendAsync(cancelRequest);
        Assert.Equal(HttpStatusCode.NoContent, cancelResponse.StatusCode);

        using var classDoc = JsonDocument.Parse(await factory.Client.GetStringAsync($"/classes/{classId}"));
        Assert.Equal(10, classDoc.RootElement.GetProperty("availableSeats").GetInt32());
        Assert.Equal(0, classDoc.RootElement.GetProperty("heldSeats").GetInt32());
    }

    [Fact]
    public async Task CannotConfirmAnotherStudentsHold()
    {
        await using var factory = await AdmissionApiFactory.CreateAsync();
        var createResponse = await factory.Client.PostAsJsonAsync("/classes", new { name = "Class 10", totalSeats = 10 });
        createResponse.EnsureSuccessStatusCode();
        using var createDoc = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var classId = createDoc.RootElement.GetProperty("id").GetString()!;

        var studentId = "12345678901234567";
        using var holdRequest = HoldRequest(HttpMethod.Post, $"/classes/{classId}/holds", studentId);
        var holdResponse = await factory.Client.SendAsync(holdRequest);
        Assert.Equal(HttpStatusCode.Created, holdResponse.StatusCode);
        using var holdDoc = JsonDocument.Parse(await holdResponse.Content.ReadAsStringAsync());
        var holdId = holdDoc.RootElement.TryGetProperty("holdId", out var hidProp)
            ? hidProp.GetString()!
            : holdDoc.RootElement.GetProperty("id").GetString()!;

        using var confirmRequest = HoldRequest(HttpMethod.Post, $"/holds/{holdId}/confirm", "99999999999999999");
        var confirmResponse = await factory.Client.SendAsync(confirmRequest);
        Assert.Equal(HttpStatusCode.NotFound, confirmResponse.StatusCode);
    }

    [Fact]
    public async Task CannotApplyTwice_SameClassOrGlobal()
    {
        await using var factory = await AdmissionApiFactory.CreateAsync();
        var createResponse = await factory.Client.PostAsJsonAsync("/classes", new { name = "Class 10", totalSeats = 10 });
        createResponse.EnsureSuccessStatusCode();
        using var createDoc = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var classId = createDoc.RootElement.GetProperty("id").GetString()!;

        var studentId = "12345678901234567";
        using var firstHoldRequest = HoldRequest(HttpMethod.Post, $"/classes/{classId}/holds", studentId);
        var firstHoldResponse = await factory.Client.SendAsync(firstHoldRequest);
        Assert.Equal(HttpStatusCode.Created, firstHoldResponse.StatusCode);

        using var secondHoldRequest = HoldRequest(HttpMethod.Post, $"/classes/{classId}/holds", studentId);
        var secondHoldResponse = await factory.Client.SendAsync(secondHoldRequest);
        Assert.Equal(HttpStatusCode.Conflict, secondHoldResponse.StatusCode);
    }

    [Fact]
    public async Task FullSeatCapacity_Returns409()
    {
        await using var factory = await AdmissionApiFactory.CreateAsync();
        var createResponse = await factory.Client.PostAsJsonAsync("/classes", new { name = "Small Class", totalSeats = 2 });
        createResponse.EnsureSuccessStatusCode();
        using var createDoc = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var classId = createDoc.RootElement.GetProperty("id").GetString()!;

        using var firstReq = HoldRequest(HttpMethod.Post, $"/classes/{classId}/holds", "11111111111111111");
        var firstResp = await factory.Client.SendAsync(firstReq);
        Assert.Equal(HttpStatusCode.Created, firstResp.StatusCode);

        using var secondReq = HoldRequest(HttpMethod.Post, $"/classes/{classId}/holds", "22222222222222222");
        var secondResp = await factory.Client.SendAsync(secondReq);
        Assert.Equal(HttpStatusCode.Created, secondResp.StatusCode);

        using var thirdReq = HoldRequest(HttpMethod.Post, $"/classes/{classId}/holds", "33333333333333333");
        var thirdResp = await factory.Client.SendAsync(thirdReq);
        Assert.Equal(HttpStatusCode.Conflict, thirdResp.StatusCode);
    }

    [Fact]
    public async Task GetMyHolds_ReturnsAllHolds()
    {
        await using var factory = await AdmissionApiFactory.CreateAsync();
        var createResponse = await factory.Client.PostAsJsonAsync("/classes", new { name = "Class 7", totalSeats = 10 });
        createResponse.EnsureSuccessStatusCode();
        using var createDoc = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var classId = createDoc.RootElement.GetProperty("id").GetString()!;

        var studentId = "12345678901234567";
        using var holdRequest = HoldRequest(HttpMethod.Post, $"/classes/{classId}/holds", studentId);
        var holdResponse = await factory.Client.SendAsync(holdRequest);
        Assert.Equal(HttpStatusCode.Created, holdResponse.StatusCode);

        using var myHoldsRequest = HoldRequest(HttpMethod.Get, "/me/holds", studentId);
        var myHoldsResponse = await factory.Client.SendAsync(myHoldsRequest);
        Assert.Equal(HttpStatusCode.OK, myHoldsResponse.StatusCode);

        using var holdsDoc = JsonDocument.Parse(await myHoldsResponse.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, holdsDoc.RootElement.ValueKind);
        var holds = holdsDoc.RootElement.EnumerateArray().ToList();
        Assert.NotEmpty(holds);
        var matching = holds.FirstOrDefault(h =>
            (h.TryGetProperty("className", out var cn) && cn.GetString() == "Class 7") &&
            (h.TryGetProperty("status", out var st) && st.GetString() == "HELD"));
        Assert.NotEqual(default, matching);
    }

    [Fact]
    public async Task ConfirmIsIdempotent()
    {
        await using var factory = await AdmissionApiFactory.CreateAsync();
        var createResponse = await factory.Client.PostAsJsonAsync("/classes", new { name = "Class 10", totalSeats = 10 });
        createResponse.EnsureSuccessStatusCode();
        using var createDoc = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var classId = createDoc.RootElement.GetProperty("id").GetString()!;

        var studentId = "12345678901234567";
        using var holdRequest = HoldRequest(HttpMethod.Post, $"/classes/{classId}/holds", studentId);
        var holdResponse = await factory.Client.SendAsync(holdRequest);
        Assert.Equal(HttpStatusCode.Created, holdResponse.StatusCode);
        using var holdDoc = JsonDocument.Parse(await holdResponse.Content.ReadAsStringAsync());
        var holdId = holdDoc.RootElement.TryGetProperty("holdId", out var hidProp)
            ? hidProp.GetString()!
            : holdDoc.RootElement.GetProperty("id").GetString()!;

        using var firstConfirm = HoldRequest(HttpMethod.Post, $"/holds/{holdId}/confirm", studentId);
        var firstResp = await factory.Client.SendAsync(firstConfirm);
        Assert.Equal(HttpStatusCode.OK, firstResp.StatusCode);
        using var firstDoc = JsonDocument.Parse(await firstResp.Content.ReadAsStringAsync());
        Assert.Equal("CONFIRMED", firstDoc.RootElement.GetProperty("status").GetString());

        using var secondConfirm = HoldRequest(HttpMethod.Post, $"/holds/{holdId}/confirm", studentId);
        var secondResp = await factory.Client.SendAsync(secondConfirm);
        Assert.Equal(HttpStatusCode.OK, secondResp.StatusCode);
        using var secondDoc = JsonDocument.Parse(await secondResp.Content.ReadAsStringAsync());
        Assert.Equal("CONFIRMED", secondDoc.RootElement.GetProperty("status").GetString());

        using var classDoc = JsonDocument.Parse(await factory.Client.GetStringAsync($"/classes/{classId}"));
        Assert.Equal(1, classDoc.RootElement.GetProperty("confirmedSeats").GetInt32());
    }

    [Fact]
    public async Task CancelConfirmedHold_Returns409()
    {
        await using var factory = await AdmissionApiFactory.CreateAsync();
        var createResponse = await factory.Client.PostAsJsonAsync("/classes", new { name = "Class 10", totalSeats = 10 });
        createResponse.EnsureSuccessStatusCode();
        using var createDoc = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var classId = createDoc.RootElement.GetProperty("id").GetString()!;

        var studentId = "12345678901234567";
        using var holdRequest = HoldRequest(HttpMethod.Post, $"/classes/{classId}/holds", studentId);
        var holdResponse = await factory.Client.SendAsync(holdRequest);
        Assert.Equal(HttpStatusCode.Created, holdResponse.StatusCode);
        using var holdDoc = JsonDocument.Parse(await holdResponse.Content.ReadAsStringAsync());
        var holdId = holdDoc.RootElement.TryGetProperty("holdId", out var hidProp)
            ? hidProp.GetString()!
            : holdDoc.RootElement.GetProperty("id").GetString()!;

        using var confirmReq = HoldRequest(HttpMethod.Post, $"/holds/{holdId}/confirm", studentId);
        var confirmResp = await factory.Client.SendAsync(confirmReq);
        Assert.Equal(HttpStatusCode.OK, confirmResp.StatusCode);

        using var cancelReq = HoldRequest(HttpMethod.Delete, $"/holds/{holdId}", studentId);
        var cancelResp = await factory.Client.SendAsync(cancelReq);
        Assert.Equal(HttpStatusCode.Conflict, cancelResp.StatusCode);
    }

    [Fact]
    public async Task MissingOrInvalidStudentId_Returns400()
    {
        await using var factory = await AdmissionApiFactory.CreateAsync();
        var createResponse = await factory.Client.PostAsJsonAsync("/classes", new { name = "Class 10", totalSeats = 10 });
        createResponse.EnsureSuccessStatusCode();
        using var createDoc = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var classId = createDoc.RootElement.GetProperty("id").GetString()!;

        // 1. Without header
        using var reqNoHeader = new HttpRequestMessage(HttpMethod.Post, $"/classes/{classId}/holds");
        var respNoHeader = await factory.Client.SendAsync(reqNoHeader);
        Assert.Equal(HttpStatusCode.BadRequest, respNoHeader.StatusCode);

        // 2. Too short
        using var reqTooShort = HoldRequest(HttpMethod.Post, $"/classes/{classId}/holds", "123");
        var respTooShort = await factory.Client.SendAsync(reqTooShort);
        Assert.Equal(HttpStatusCode.BadRequest, respTooShort.StatusCode);

        // 3. Non-digit
        using var reqNonDigit = HoldRequest(HttpMethod.Post, $"/classes/{classId}/holds", "1234567890123456A");
        var respNonDigit = await factory.Client.SendAsync(reqNonDigit);
        Assert.Equal(HttpStatusCode.BadRequest, respNonDigit.StatusCode);
    }

    // MANDATORY CONCURRENCY TEST
    [Fact]
    public async Task ConcurrentHoldRequests_NeverExceedCapacity()
    {
        await using var factory = await AdmissionApiFactory.CreateAsync();

        // Create class with only 5 seats
        var classResponse = await factory.Client.PostAsJsonAsync("/classes", new { name = "Concurrency Test Class", totalSeats = 5 });
        classResponse.EnsureSuccessStatusCode();
        using var classDoc = JsonDocument.Parse(await classResponse.Content.ReadAsStringAsync());
        var classId = classDoc.RootElement.GetProperty("id").GetString()!;

        // Fire 50 concurrent hold requests from different students
        const int concurrentRequests = 50;
        using var barrier = new Barrier(concurrentRequests); // synchronize start
        var tasks = Enumerable.Range(0, concurrentRequests).Select(i => Task.Run(async () =>
        {
            var studentId = $"{i:D17}"; // "00000000000000000", "00000000000000001", etc.
            barrier.SignalAndWait(); // wait for all tasks to be ready
            var request = new HttpRequestMessage(HttpMethod.Post, $"/classes/{classId}/holds");
            request.Headers.Add("X-Student-Id", studentId);
            return await factory.Client.SendAsync(request);
        })).ToList();

        var responses = await Task.WhenAll(tasks);
        var statusCodes = responses.Select(r => (int)r.StatusCode).ToList();

        var successCount = statusCodes.Count(s => s == 201);
        var conflictCount = statusCodes.Count(s => s == 409);

        // CRITICAL: exactly 5 should succeed, rest should get 409
        Assert.Equal(5, successCount);
        Assert.Equal(45, conflictCount);

        // Verify DB state is consistent
        using var classDoc2 = JsonDocument.Parse(await factory.Client.GetStringAsync($"/classes/{classId}"));
        var heldSeats = classDoc2.RootElement.GetProperty("heldSeats").GetInt32();
        var availableSeats = classDoc2.RootElement.GetProperty("availableSeats").GetInt32();
        Assert.Equal(5, heldSeats);
        Assert.Equal(0, availableSeats);

        // Dispose all responses
        foreach (var r in responses) r.Dispose();
    }

    [Fact]
    public async Task GetHold_WithAndWithoutHeader_AndOwnershipEnforced()
    {
        await using var factory = await AdmissionApiFactory.CreateAsync();
        var createResponse = await factory.Client.PostAsJsonAsync("/classes", new { name = "Class 8", totalSeats = 10 });
        createResponse.EnsureSuccessStatusCode();
        using var createDoc = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var classId = createDoc.RootElement.GetProperty("id").GetString()!;

        var studentId = "12345678901234567";
        using var holdRequest = HoldRequest(HttpMethod.Post, $"/classes/{classId}/holds", studentId);
        var holdResponse = await factory.Client.SendAsync(holdRequest);
        Assert.Equal(HttpStatusCode.Created, holdResponse.StatusCode);
        using var holdDoc = JsonDocument.Parse(await holdResponse.Content.ReadAsStringAsync());
        var holdId = holdDoc.RootElement.GetProperty("holdId").GetString()!;

        // 1. With correct student header -> 200
        using var withHeader = HoldRequest(HttpMethod.Get, $"/holds/{holdId}", studentId);
        var respWithHeader = await factory.Client.SendAsync(withHeader);
        Assert.Equal(HttpStatusCode.OK, respWithHeader.StatusCode);
        using var docWithHeader = JsonDocument.Parse(await respWithHeader.Content.ReadAsStringAsync());
        Assert.Equal(holdId, docWithHeader.RootElement.GetProperty("holdId").GetString());
        Assert.Equal("HELD", docWithHeader.RootElement.GetProperty("status").GetString());

        // 2. Without header -> 200 (spec 1.4 table 5 omitted header requirement)
        using var respNoHeader = await factory.Client.GetAsync($"/holds/{holdId}");
        Assert.Equal(HttpStatusCode.OK, respNoHeader.StatusCode);
        using var docNoHeader = JsonDocument.Parse(await respNoHeader.Content.ReadAsStringAsync());
        Assert.Equal(holdId, docNoHeader.RootElement.GetProperty("holdId").GetString());

        // 3. With different student header -> 404 (ADR-11 information hiding)
        using var withWrongHeader = HoldRequest(HttpMethod.Get, $"/holds/{holdId}", "99999999999999999");
        var respWrongHeader = await factory.Client.SendAsync(withWrongHeader);
        Assert.Equal(HttpStatusCode.NotFound, respWrongHeader.StatusCode);
    }

    [Fact]
    public async Task CancelExpiredHold_Returns409()
    {
        await using var factory = await AdmissionApiFactory.CreateAsync();
        var createResponse = await factory.Client.PostAsJsonAsync("/classes", new { name = "Short Hold Class", totalSeats = 10 });
        createResponse.EnsureSuccessStatusCode();
        using var createDoc = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var classId = createDoc.RootElement.GetProperty("id").GetString()!;

        var studentId = "12345678901234567";
        using var holdRequest = HoldRequest(HttpMethod.Post, $"/classes/{classId}/holds", studentId);
        var holdResponse = await factory.Client.SendAsync(holdRequest);
        Assert.Equal(HttpStatusCode.Created, holdResponse.StatusCode);
        using var holdDoc = JsonDocument.Parse(await holdResponse.Content.ReadAsStringAsync());
        var holdId = holdDoc.RootElement.GetProperty("holdId").GetString()!;

        // Directly expire the hold in the database to simulate time passing
        await using (var conn = new Npgsql.NpgsqlConnection(factory.ConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = new Npgsql.NpgsqlCommand(
                "UPDATE holds SET expires_at = NOW() - INTERVAL '1 minute' WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("id", holdId);
            await cmd.ExecuteNonQueryAsync();
        }

        // Cancel attempt should return 409 Conflict per Assumption 11
        using var cancelReq = HoldRequest(HttpMethod.Delete, $"/holds/{holdId}", studentId);
        var cancelResp = await factory.Client.SendAsync(cancelReq);
        Assert.Equal(HttpStatusCode.Conflict, cancelResp.StatusCode);
    }
}
