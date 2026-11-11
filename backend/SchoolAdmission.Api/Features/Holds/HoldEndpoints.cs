using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Infrastructure;

namespace SchoolAdmission.Api.Features.Holds;

public static class HoldEndpoints
{
    public static IEndpointRouteBuilder MapHoldEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/classes/{classId}/holds", CreateHoldAsync);
        endpoints.MapGet("/holds/{holdId}", GetHoldAsync);
        endpoints.MapPost("/holds/{holdId}/confirm", ConfirmHoldAsync);
        endpoints.MapDelete("/holds/{holdId}", CancelHoldAsync);
        endpoints.MapGet("/me/holds", ListMyHoldsAsync);
        return endpoints;
    }

    private static async Task<Created<HoldResponse>> CreateHoldAsync(
        string classId,
        HttpRequest httpRequest,
        AdmissionDbContext db,
        IConfiguration config,
        CancellationToken cancellationToken)
    {
        var studentId = StudentIdHelper.TryGetStudentId(httpRequest);
        if (studentId is null)
            throw InvalidStudentId();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var @class = await db.Classes
            .FromSqlRaw("SELECT * FROM classes WHERE id = {0} FOR UPDATE", classId)
            .SingleOrDefaultAsync(cancellationToken);

        if (@class is null)
            throw new AdmissionFailure(StatusCodes.Status404NotFound, "The requested class was not found.");

        var holdDurationSeconds = config.GetValue<int>("HOLD_DURATION_SECONDS", 600);
        var now = DateTimeOffset.UtcNow;

        // Clean up any overdue holds for this class so seats are immediately freed
        var classOverdue = await db.Holds
            .FromSqlRaw("SELECT * FROM holds WHERE class_id = {0} AND status = 'HELD' AND expires_at < {1} FOR UPDATE", classId, now)
            .ToListAsync(cancellationToken);

        foreach (var overdue in classOverdue)
        {
            overdue.Status = "EXPIRED";
            @class.HeldSeats = Math.Max(0, @class.HeldSeats - 1);
        }

        // Clean up any overdue holds for this student so they can re-apply immediately
        var studentOverdue = await db.Holds
            .FromSqlRaw("SELECT * FROM holds WHERE student_id = {0} AND status = 'HELD' AND expires_at < {1} FOR UPDATE", studentId, now)
            .ToListAsync(cancellationToken);

        foreach (var overdue in studentOverdue)
        {
            overdue.Status = "EXPIRED";
            if (overdue.ClassId != classId)
            {
                var overdueClass = await db.Classes
                    .FromSqlRaw("SELECT * FROM classes WHERE id = {0} FOR UPDATE", overdue.ClassId)
                    .SingleOrDefaultAsync(cancellationToken);
                if (overdueClass is not null)
                {
                    overdueClass.HeldSeats = Math.Max(0, overdueClass.HeldSeats - 1);
                }
            }
        }

        if (@class.TotalSeats - @class.HeldSeats - @class.ConfirmedSeats <= 0)
            throw new AdmissionFailure(StatusCodes.Status409Conflict, "No available seats.");

        var hold = new HoldEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            ClassId = classId,
            ClassName = @class.Name,
            StudentId = studentId,
            Status = "HELD",
            ExpiresAt = now.AddSeconds(holdDurationSeconds),
            CreatedAt = now
        };

        @class.HeldSeats++;
        db.Holds.Add(hold);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { ConstraintName: "uq_student_active" })
        {
            throw new AdmissionFailure(StatusCodes.Status409Conflict, "You already have an active application or confirmed admission.");
        }

        var connectionString = db.Database.GetConnectionString() ?? config.GetConnectionString("Database")!;
        await NotifyAdmissionClassesAsync(connectionString);

        return TypedResults.Created(
            $"/holds/{hold.Id}",
            new HoldResponse(hold.Id, hold.ClassId, hold.StudentId, hold.Status, hold.ExpiresAt));
    }

    private static async Task<Ok<HoldStatusResponse>> GetHoldAsync(
        string holdId,
        HttpRequest httpRequest,
        AdmissionDbContext db,
        CancellationToken cancellationToken)
    {
        var rawHeader = httpRequest.Headers["X-Student-Id"].FirstOrDefault();
        string? studentId = null;
        if (!string.IsNullOrEmpty(rawHeader))
        {
            if (rawHeader.Length != 17 || !rawHeader.All(char.IsAsciiDigit))
                throw InvalidStudentId();
            studentId = rawHeader;
        }

        var hold = await db.Holds.AsNoTracking()
            .Where(h => h.Id == holdId)
            .SingleOrDefaultAsync(cancellationToken);

        if (hold is null || (studentId is not null && hold.StudentId != studentId))
            throw new AdmissionFailure(StatusCodes.Status404NotFound, "The requested hold was not found.");

        var effectiveStatus = (hold.Status == "HELD" && hold.ExpiresAt < DateTimeOffset.UtcNow)
            ? "EXPIRED"
            : hold.Status;

        return TypedResults.Ok(new HoldStatusResponse(hold.Id, hold.ClassId, effectiveStatus, hold.ExpiresAt));
    }

    private static async Task<Ok<ConfirmResponse>> ConfirmHoldAsync(
        string holdId,
        HttpRequest httpRequest,
        AdmissionDbContext db,
        IConfiguration config,
        CancellationToken cancellationToken)
    {
        var studentId = StudentIdHelper.TryGetStudentId(httpRequest);
        if (studentId is null)
            throw InvalidStudentId();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var hold = await db.Holds
            .FromSqlRaw("SELECT * FROM holds WHERE id = {0} FOR UPDATE", holdId)
            .SingleOrDefaultAsync(cancellationToken);

        if (hold is null || hold.StudentId != studentId)
            throw new AdmissionFailure(StatusCodes.Status404NotFound, "The requested hold was not found.");

        if (hold.Status == "CONFIRMED")
        {
            return TypedResults.Ok(new ConfirmResponse(hold.Id, "CONFIRMED"));
        }

        var now = DateTimeOffset.UtcNow;
        if (hold.Status == "EXPIRED" || (hold.Status == "HELD" && hold.ExpiresAt < now))
        {
            if (hold.Status == "HELD")
            {
                hold.Status = "EXPIRED";
                var @class = await db.Classes
                    .FromSqlRaw("SELECT * FROM classes WHERE id = {0} FOR UPDATE", hold.ClassId)
                    .SingleOrDefaultAsync(cancellationToken);
                if (@class is not null)
                {
                    @class.HeldSeats = Math.Max(0, @class.HeldSeats - 1);
                }
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            throw new AdmissionFailure(StatusCodes.Status409Conflict, "Hold has expired.");
        }

        if (hold.Status == "CANCELLED")
        {
            throw new AdmissionFailure(StatusCodes.Status409Conflict, "Hold was cancelled.");
        }

        if (hold.Status == "HELD")
        {
            var @class = await db.Classes
                .FromSqlRaw("SELECT * FROM classes WHERE id = {0} FOR UPDATE", hold.ClassId)
                .SingleOrDefaultAsync(cancellationToken);

            if (@class is null)
                throw new AdmissionFailure(StatusCodes.Status404NotFound, "The requested class was not found.");

            @class.HeldSeats--;
            @class.ConfirmedSeats++;
            hold.Status = "CONFIRMED";

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var connectionString = db.Database.GetConnectionString() ?? config.GetConnectionString("Database")!;
            await NotifyAdmissionClassesAsync(connectionString);

            return TypedResults.Ok(new ConfirmResponse(hold.Id, "CONFIRMED"));
        }

        throw new AdmissionFailure(StatusCodes.Status409Conflict, $"Invalid hold status: {hold.Status}");
    }

    private static async Task<NoContent> CancelHoldAsync(
        string holdId,
        HttpRequest httpRequest,
        AdmissionDbContext db,
        IConfiguration config,
        CancellationToken cancellationToken)
    {
        var studentId = StudentIdHelper.TryGetStudentId(httpRequest);
        if (studentId is null)
            throw InvalidStudentId();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var hold = await db.Holds
            .FromSqlRaw("SELECT * FROM holds WHERE id = {0} FOR UPDATE", holdId)
            .SingleOrDefaultAsync(cancellationToken);

        if (hold is null || hold.StudentId != studentId)
            throw new AdmissionFailure(StatusCodes.Status404NotFound, "The requested hold was not found.");

        if (hold.Status == "CANCELLED")
        {
            return TypedResults.NoContent();
        }

        if (hold.Status == "CONFIRMED")
        {
            throw new AdmissionFailure(StatusCodes.Status409Conflict, "Confirmed admissions cannot be cancelled.");
        }

        var now = DateTimeOffset.UtcNow;
        if (hold.Status == "EXPIRED" || (hold.Status == "HELD" && hold.ExpiresAt < now))
        {
            if (hold.Status == "HELD")
            {
                hold.Status = "EXPIRED";
                var @class = await db.Classes
                    .FromSqlRaw("SELECT * FROM classes WHERE id = {0} FOR UPDATE", hold.ClassId)
                    .SingleOrDefaultAsync(cancellationToken);
                if (@class is not null)
                {
                    @class.HeldSeats = Math.Max(0, @class.HeldSeats - 1);
                }
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            throw new AdmissionFailure(StatusCodes.Status409Conflict, "Hold has already expired.");
        }

        if (hold.Status == "HELD")
        {
            var @class = await db.Classes
                .FromSqlRaw("SELECT * FROM classes WHERE id = {0} FOR UPDATE", hold.ClassId)
                .SingleOrDefaultAsync(cancellationToken);

            if (@class is null)
                throw new AdmissionFailure(StatusCodes.Status404NotFound, "The requested class was not found.");

            @class.HeldSeats--;
            hold.Status = "CANCELLED";
            hold.CancelledAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var connectionString = db.Database.GetConnectionString() ?? config.GetConnectionString("Database")!;
            await NotifyAdmissionClassesAsync(connectionString);

            return TypedResults.NoContent();
        }

        throw new AdmissionFailure(StatusCodes.Status409Conflict, $"Invalid hold status: {hold.Status}");
    }

    private static async Task<Ok<List<StudentHoldResponse>>> ListMyHoldsAsync(
        HttpRequest httpRequest,
        AdmissionDbContext db,
        CancellationToken cancellationToken)
    {
        var studentId = StudentIdHelper.TryGetStudentId(httpRequest);
        if (studentId is null)
            throw InvalidStudentId();

        var holds = await db.Holds.AsNoTracking()
            .Where(h => h.StudentId == studentId)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var responses = holds.Select(h =>
        {
            var effectiveStatus = (h.Status == "HELD" && h.ExpiresAt < now)
                ? "EXPIRED"
                : h.Status;
            return new StudentHoldResponse(h.Id, h.ClassId, h.ClassName, effectiveStatus, h.ExpiresAt);
        }).ToList();

        return TypedResults.Ok(responses);
    }

    private static AdmissionFailure InvalidStudentId()
        => new(StatusCodes.Status400BadRequest, "Provide a valid 17-digit student ID via the X-Student-Id header.");

    internal static async Task NotifyAdmissionClassesAsync(string connectionString, ILogger? logger = null)
    {
        try
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("NOTIFY admission_classes", conn);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to send NOTIFY admission_classes");
        }
    }
}
