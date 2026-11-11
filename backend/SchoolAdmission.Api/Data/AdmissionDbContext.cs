using Microsoft.EntityFrameworkCore;

namespace SchoolAdmission.Api.Data;

// Domain entities and their migrations will be added with the admission workflows.
public sealed class AdmissionDbContext(DbContextOptions<AdmissionDbContext> options)
    : DbContext(options);
