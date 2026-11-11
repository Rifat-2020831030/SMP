namespace SchoolAdmission.Api.Features.Holds;

public sealed class HoldEntity
{
    public required string Id { get; init; }
    public required string ClassId { get; set; }
    public required string ClassName { get; set; }
    public required string StudentId { get; set; }
    public required string Status { get; set; } // "HELD", "CONFIRMED", "EXPIRED", "CANCELLED"
    public required DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
