using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;

namespace SchoolAdmission.Api.Features.Classes;

public sealed class ClassQueries(AdmissionDbContext db)
{
    public async Task<IReadOnlyList<ClassSummary>> ListAsync(CancellationToken cancellationToken)
    {
        return await db.Classes.AsNoTracking()
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
    }

    public Task<ClassSummary?> GetAsync(string classId, CancellationToken cancellationToken)
    {
        return db.Classes.AsNoTracking()
            .Where(item => item.Id == classId)
            .Select(item => new ClassSummary(
                item.Id,
                item.Name,
                item.TotalSeats,
                item.HeldSeats,
                item.ConfirmedSeats,
                item.TotalSeats - item.HeldSeats - item.ConfirmedSeats))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
