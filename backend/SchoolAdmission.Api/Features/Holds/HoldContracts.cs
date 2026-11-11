namespace SchoolAdmission.Api.Features.Holds;

public sealed record HoldResponse(string HoldId, string ClassId, string StudentId, string Status, DateTimeOffset ExpiresAt);
public sealed record HoldStatusResponse(string HoldId, string ClassId, string Status, DateTimeOffset ExpiresAt);
public sealed record ConfirmResponse(string HoldId, string Status);
public sealed record StudentHoldResponse(string HoldId, string ClassId, string ClassName, string Status, DateTimeOffset ExpiresAt);
