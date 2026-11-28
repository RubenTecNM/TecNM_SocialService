using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TecNM.Api.Core.Entities;

namespace TecNM.Api.Data.Configurations;

public sealed class ActivityTypeConfiguration : AuditableEntityConfiguration<ActivityType>
{
    public override void Configure(EntityTypeBuilder<ActivityType> builder)
    {
        base.Configure(builder);

        builder.ToTable("activity_types");
        builder.HasKey(a => a.Id).HasName("pk_activity_types");
        builder.Property(a => a.Code).HasColumnName("code").HasMaxLength(32).IsRequired();
        builder.Property(a => a.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(a => a.AppliesTo).HasColumnName("applies_to").HasMaxLength(20).HasDefaultValue("both").IsRequired();
        builder.Property(a => a.Description).HasColumnName("description");
        builder.Property(a => a.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();

        builder.HasIndex(a => a.Code)
            .IsUnique()
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("uq_activity_types__code");

        builder.HasIndex(a => a.AppliesTo)
            .HasDatabaseName("idx_activity_types__applies_to");
    }

    protected override string GetTableName() => "activity_types";
}
