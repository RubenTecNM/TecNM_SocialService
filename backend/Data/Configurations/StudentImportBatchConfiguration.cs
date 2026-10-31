using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TecNM.Api.Core.Entities;

namespace TecNM.Api.Data.Configurations;

public sealed class StudentImportBatchConfiguration : AuditableEntityConfiguration<StudentImportBatch>
{
    public override void Configure(EntityTypeBuilder<StudentImportBatch> builder)
    {
        base.Configure(builder);

        builder.ToTable("student_import_batches");
        builder.HasKey(b => b.Id).HasName("pk_student_import_batches");

        builder.Property(b => b.PeriodId).HasColumnName("period_id").IsRequired();
        builder.Property(b => b.FileName).HasColumnName("file_name").HasMaxLength(255).IsRequired();
        builder.Property(b => b.FileUrl).HasColumnName("file_url").IsRequired();
        builder.Property(b => b.TotalRecords).HasColumnName("total_records").HasDefaultValue(0).IsRequired();
        builder.Property(b => b.ImportedRecords).HasColumnName("imported_records").HasDefaultValue(0).IsRequired();
        builder.Property(b => b.FailedRecords).HasColumnName("failed_records").HasDefaultValue(0).IsRequired();
        builder.Property(b => b.Status).HasColumnName("status").HasMaxLength(32).HasDefaultValue("pending").IsRequired();
        builder.Property(b => b.Notes).HasColumnName("notes");

        builder.HasOne(b => b.Period)
            .WithMany()
            .HasForeignKey(b => b.PeriodId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_student_import_batches__academic_periods");

        builder.HasIndex(b => b.PeriodId).HasDatabaseName("idx_student_import_batches__period_id");
    }

    protected override string GetTableName() => "student_import_batches";
}

