namespace SchoolAdmission.Api.Features.Classes;

public sealed record CreateClassRequest(string Name, int TotalSeats);
public sealed record CreatedClassResponse(string Id, string Name, int TotalSeats);
public sealed record ClassSummary(string Id, string Name, int TotalSeats, int HeldSeats, int ConfirmedSeats, int AvailableSeats);
