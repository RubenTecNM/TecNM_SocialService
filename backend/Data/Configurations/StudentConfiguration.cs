using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TecNM.Api.Core.Entities;

namespace TecNM.Api.Data.Configurations;

public sealed class StudentConfiguration : AuditableEntityConfiguration<Student>
{
    public override void Configure(EntityTypeBuilder<Student> builder)
    {
        base.Configure(builder);

        builder.ToTable("students");
        builder.HasKey(s => s.Id).HasName("pk_students");

        builder.Property(s => s.UserId).HasColumnName("user_id");
        builder.Property(s => s.CareerId).HasColumnName("career_id").IsRequired();
        builder.Property(s => s.ImportBatchId).HasColumnName("import_batch_id");
        builder.Property(s => s.ControlNumber).HasColumnName("control_number").HasMaxLength(32).IsRequired();
        builder.Property(s => s.Curp).HasColumnName("curp").HasMaxLength(18);
        builder.Property(s => s.FirstSurname).HasColumnName("first_surname").HasMaxLength(70).IsRequired();
        builder.Property(s => s.SecondSurname).HasColumnName("second_surname").HasMaxLength(70);
        builder.Property(s => s.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
        builder.Property(s => s.Gender).HasColumnName("gender").HasMaxLength(1);
        builder.Property(s => s.Shift).HasColumnName("shift").HasMaxLength(16).HasDefaultValue("morning").IsRequired();
        builder.Property(s => s.HasDisability).HasColumnName("has_disability").HasDefaultValue(false).IsRequired();
        builder.Property(s => s.DisabilityDescription).HasColumnName("disability_description").HasMaxLength(150);
        builder.Property(s => s.InstitutionalEmail).HasColumnName("institutional_email").HasMaxLength(255);
        builder.Property(s => s.PersonalEmail).HasColumnName("personal_email").HasMaxLength(255);
        builder.Property(s => s.PhoneNumber).HasColumnName("phone_number").HasMaxLength(20);
        builder.Property(s => s.CurrentSemester).HasColumnName("current_semester").HasDefaultValue(1).IsRequired();
        builder.Property(s => s.CompletedCredits).HasColumnName("completed_credits").HasDefaultValue(0).IsRequired();
        builder.Property(s => s.CurriculumProgressPercentage)
            .HasColumnName("curriculum_progress_percentage")
            .HasPrecision(5, 2)
            .HasDefaultValue(0.00m)
            .IsRequired();
        builder.Property(s => s.IsEnrolled).HasColumnName("is_enrolled").HasDefaultValue(true).IsRequired();
        builder.Property(s => s.HasValidImss).HasColumnName("has_valid_imss").HasDefaultValue(false).IsRequired();
        builder.Property(s => s.ImssValidityFileUrl).HasColumnName("imss_validity_file_url");
        builder.Property(s => s.ImssValidatedAt).HasColumnName("imss_validated_at");
        builder.Property(s => s.ImssValidatedBy).HasColumnName("imss_validated_by");

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_students__users");

        builder.HasOne(s => s.Career)
            .WithMany()
            .HasForeignKey(s => s.CareerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_students__careers");

        builder.HasOne(s => s.ImportBatch)
            .WithMany()
            .HasForeignKey(s => s.ImportBatchId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_students__student_import_batches");

        builder.HasOne(s => s.ImssValidator)
            .WithMany()
            .HasForeignKey(s => s.ImssValidatedBy)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_students__imss_validator");

        builder.HasIndex(s => s.ControlNumber)
            .IsUnique()
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("uq_students__control_number");

        builder.HasIndex(s => s.UserId)
            .IsUnique()
            .HasFilter("user_id IS NOT NULL AND deleted_at IS NULL")
            .HasDatabaseName("uq_students__user_id");

        builder.HasIndex(s => s.CareerId).HasDatabaseName("idx_students__career_id");
        builder.HasIndex(s => s.ImportBatchId).HasDatabaseName("idx_students__import_batch_id");
        builder.HasIndex(s => s.Shift).HasDatabaseName("idx_students__shift");
        builder.HasIndex(s => s.HasDisability).HasDatabaseName("idx_students__has_disability");
    }

    protected override string GetTableName() => "students";
}

