using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Features.Classes;
using SchoolAdmission.Api.Features.Holds;

namespace SchoolAdmission.Api.Data;

public sealed class AdmissionDbContext(DbContextOptions<AdmissionDbContext> options)
    : DbContext(options)
{
    public DbSet<ClassEntity> Classes => Set<ClassEntity>();
    public DbSet<HoldEntity> Holds => Set<HoldEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClassEntity>(entity =>
        {
            entity.ToTable("classes", table =>
            {
                table.HasCheckConstraint("ck_classes_name_length", "char_length(name) BETWEEN 1 AND 200");
                table.HasCheckConstraint("ck_classes_name_trimmed", "name = btrim(name)");
                table.HasCheckConstraint("ck_classes_total_seats", "total_seats > 0");
                table.HasCheckConstraint("ck_classes_nonnegative", "held_seats >= 0 AND confirmed_seats >= 0");
                table.HasCheckConstraint("ck_classes_capacity", "held_seats + confirmed_seats <= total_seats");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id").HasColumnType("text").ValueGeneratedNever();
            entity.Property(item => item.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(item => item.NormalizedName)
                .HasColumnName("normalized_name")
                .HasComputedColumnSql("lower(name)", stored: true);
            entity.Property(item => item.TotalSeats).HasColumnName("total_seats");
            entity.Property(item => item.HeldSeats).HasColumnName("held_seats").HasDefaultValue(0);
            entity.Property(item => item.ConfirmedSeats).HasColumnName("confirmed_seats").HasDefaultValue(0);
            entity.HasIndex(item => item.NormalizedName).IsUnique().HasDatabaseName("uq_class_name");
        });

        modelBuilder.Entity<HoldEntity>(entity =>
        {
            entity.ToTable("holds");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id").HasColumnType("text").ValueGeneratedNever();
            entity.Property(item => item.ClassId).HasColumnName("class_id").HasColumnType("text").IsRequired();
            entity.Property(item => item.ClassName).HasColumnName("class_name").HasColumnType("text").IsRequired();
            entity.Property(item => item.StudentId).HasColumnName("student_id").HasColumnType("text").IsRequired();
            entity.Property(item => item.Status).HasColumnName("status").HasColumnType("text").IsRequired();
            entity.Property(item => item.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(item => item.CancelledAt).HasColumnName("cancelled_at").HasColumnType("timestamp with time zone");
            entity.Property(item => item.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("NOW()");

            entity.HasOne<ClassEntity>()
                .WithMany()
                .HasForeignKey(item => item.ClassId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex([nameof(HoldEntity.StudentId)], "uq_student_active")
                .IsUnique()
                .HasFilter("status IN ('HELD', 'CONFIRMED')");

            entity.HasIndex([nameof(HoldEntity.StudentId)], "ix_holds_student_id");

            entity.HasIndex([nameof(HoldEntity.ClassId), nameof(HoldEntity.Status)], "ix_holds_class_id_status");
        });
    }
}
