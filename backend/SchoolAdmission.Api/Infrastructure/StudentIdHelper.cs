namespace SchoolAdmission.Api.Infrastructure;

public static class StudentIdHelper
{
    public static string? TryGetStudentId(HttpRequest request)
    {
        var value = request.Headers["X-Student-Id"].FirstOrDefault();
        if (string.IsNullOrEmpty(value) || value.Length != 17 || !value.All(char.IsAsciiDigit))
            return null;
        return value;
    }
}
