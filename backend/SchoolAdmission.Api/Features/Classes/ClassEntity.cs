namespace SchoolAdmission.Api.Features.Classes;

public sealed class ClassEntity
{
    public required string Id { get; init; }
    public required string Name { get; set; }
    public string NormalizedName { get; private set; } = string.Empty;
    public int TotalSeats { get; set; }
    public int HeldSeats { get; set; }
    public int ConfirmedSeats { get; set; }
}
