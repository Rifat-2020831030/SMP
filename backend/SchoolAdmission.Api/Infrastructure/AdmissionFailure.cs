namespace SchoolAdmission.Api.Infrastructure;

public sealed class AdmissionFailure(int status, string detail) : Exception(detail)
{
    public int Status { get; } = status;
    public string Detail { get; } = detail;
}
