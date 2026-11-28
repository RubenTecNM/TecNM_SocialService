using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TecNM.Api.Core.Entities;

namespace TecNM.Api.Data.Configurations;

public sealed class AcademicPeriodConfiguration : AuditableEntityConfiguration<AcademicPeriod>
{
    public override void Configure(EntityTypeBuilder<AcademicPeriod> builder)
    {
        base.Configure(builder);

        builder.ToTable("academic_periods");
        builder.HasKey(ap => ap.Id).HasName("pk_academic_periods");
        builder.Property(ap => ap.Code).HasColumnName("code").HasMaxLength(32).IsRequired();
        builder.Property(ap => ap.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(ap => ap.SchoolYear).HasColumnName("school_year").HasMaxLength(16).IsRequired();
        builder.Property(ap => ap.StartDate).HasColumnName("start_date").IsRequired();
        builder.Property(ap => ap.EndDate).HasColumnName("end_date").IsRequired();
        builder.Property(ap => ap.IsActive).HasColumnName("is_active").HasDefaultValue(false).IsRequired();

        builder.HasIndex(ap => ap.Code)
            .IsUnique()
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("uq_academic_periods__code");

        builder.HasIndex(ap => ap.StartDate)
            .HasDatabaseName("idx_academic_periods__start_date");
    }

    protected override string GetTableName() => "academic_periods";
}
