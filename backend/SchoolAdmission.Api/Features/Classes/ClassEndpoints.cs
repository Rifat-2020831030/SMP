using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text;
using System.Text.Json;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Infrastructure;

namespace SchoolAdmission.Api.Features.Classes;

public static class ClassEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapClassEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/classes");
        group.MapPost("", CreateAsync);
        group.MapGet("", ListAsync);
        group.MapGet("/stream", StreamAsync);
        group.MapGet("/{classId}", GetAsync);
        return endpoints;
    }

    private static async Task<Created<CreatedClassResponse>> CreateAsync(
        HttpRequest httpRequest,
        AdmissionDbContext db,
        CancellationToken cancellationToken)
    {
        CreateClassRequest request;
        try
        {
            using var document = await JsonDocument.ParseAsync(httpRequest.Body, cancellationToken: cancellationToken);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("name", out var nameValue) || nameValue.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("totalSeats", out var seatsValue) || seatsValue.ValueKind != JsonValueKind.Number ||
                !seatsValue.TryGetInt32(out var totalSeats))
                throw InvalidClassRequest();

            request = new CreateClassRequest(nameValue.GetString()!, totalSeats);
        }
        catch (JsonException)
        {
            throw InvalidClassRequest();
        }

        var name = request.Name.Trim();
        if (string.IsNullOrEmpty(name) || name.EnumerateRunes().Count() > 200 || request.TotalSeats <= 0)
            throw new AdmissionFailure(StatusCodes.Status400BadRequest, "Provide a class name of 1 to 200 characters and a positive seat capacity.");

        var entity = new ClassEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            TotalSeats = request.TotalSeats
        };
        db.Classes.Add(entity);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { ConstraintName: "uq_class_name" })
        {
            throw new AdmissionFailure(StatusCodes.Status409Conflict, "A class with this name already exists.");
        }

        return TypedResults.Created($"/classes/{entity.Id}", new CreatedClassResponse(entity.Id, entity.Name, entity.TotalSeats));
    }

    private static AdmissionFailure InvalidClassRequest()
        => new(StatusCodes.Status400BadRequest, "Provide a class name of 1 to 200 characters and a positive seat capacity.");

    private static async Task<IReadOnlyList<ClassSummary>> ListAsync(ClassQueries queries, CancellationToken cancellationToken)
        => await queries.ListAsync(cancellationToken);

    private static async Task StreamAsync(
        AdmissionDbContext db,
        IConfiguration config,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        response.Headers.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers.Connection = "keep-alive";

        var connectionString = db.Database.GetConnectionString()
            ?? config.GetConnectionString("Database")
            ?? throw new InvalidOperationException("ConnectionStrings:Database is required.");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var cmd = new NpgsqlCommand("LISTEN admission_classes", connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        async Task SendSnapshotAsync()
        {
            var classes = await db.Classes.AsNoTracking()
                .OrderBy(item => item.Name)
                .ThenBy(item => item.Id)
                .Select(item => new ClassSummary(
                    item.Id,
                    item.Name,
                    item.TotalSeats,
                    item.HeldSeats,
                    item.ConfirmedSeats,
                    item.TotalSeats - item.HeldSeats - item.ConfirmedSeats))
                .ToListAsync(cancellationToken);

            var json = JsonSerializer.Serialize(classes, JsonOptions);
            await response.WriteAsync($"data: {json}\n\n", cancellationToken);
            await response.Body.FlushAsync(cancellationToken);
        }

        await SendSnapshotAsync();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await connection.WaitAsync(TimeSpan.FromSeconds(3), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                await SendSnapshotAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException)
            {
                break;
            }
        }
    }

    private static async Task<Ok<ClassSummary>> GetAsync(
        string classId,
        ClassQueries queries,
        CancellationToken cancellationToken)
    {
        var summary = await queries.GetAsync(classId, cancellationToken);
        if (summary is null)
            throw new AdmissionFailure(StatusCodes.Status404NotFound, "The requested class was not found.");
        return TypedResults.Ok(summary);
    }
}
