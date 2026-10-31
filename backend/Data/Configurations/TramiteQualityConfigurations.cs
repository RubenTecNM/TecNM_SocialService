using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TecNM.Api.Core.Entities;

namespace TecNM.Api.Data.Configurations;

public class TramiteStatusLogConfiguration : IEntityTypeConfiguration<TramiteStatusLog>
{
    public void Configure(EntityTypeBuilder<TramiteStatusLog> builder)
    {
        builder.ToTable("tramite_status_logs");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.TramiteType).HasColumnName("tramite_type").HasMaxLength(32).IsRequired();
        builder.Property(l => l.TramiteId).HasColumnName("tramite_id").IsRequired();
        builder.Property(l => l.PreviousStatus).HasColumnName("previous_status").HasMaxLength(32);
        builder.Property(l => l.NewStatus).HasColumnName("new_status").HasMaxLength(32).IsRequired();
        builder.Property(l => l.Reason).HasColumnName("reason");
        builder.Property(l => l.ChangedBy).HasColumnName("changed_by").IsRequired();

        builder.Property(l => l.CreatedBy).HasColumnName("created_by");
        builder.Property(l => l.UpdatedBy).HasColumnName("updated_by");
        builder.Property(l => l.DeletedBy).HasColumnName("deleted_by");
        builder.Property(l => l.CreatedAt).HasColumnName("created_at");
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at");
        builder.Property(l => l.DeletedAt).HasColumnName("deleted_at");

        builder.HasOne(l => l.ChangedByUser)
               .WithMany()
               .HasForeignKey(l => l.ChangedBy)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(l => l.DeletedAt == null);
    }
}

public class NonConformanceConfiguration : IEntityTypeConfiguration<NonConformance>
{
    public void Configure(EntityTypeBuilder<NonConformance> builder)
    {
        builder.ToTable("non_conformances");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasColumnName("id");
        builder.Property(n => n.TramiteType).HasColumnName("tramite_type").HasMaxLength(32).IsRequired();
        builder.Property(n => n.TramiteId).HasColumnName("tramite_id").IsRequired();
        builder.Property(n => n.StudentId).HasColumnName("student_id").IsRequired();
        builder.Property(n => n.PeriodId).HasColumnName("period_id").IsRequired();
        builder.Property(n => n.IncidentCode).HasColumnName("incident_code").HasMaxLength(64).IsRequired();
        builder.Property(n => n.Category).HasColumnName("category").HasMaxLength(32).IsRequired();
        builder.Property(n => n.Description).HasColumnName("description").IsRequired();
        builder.Property(n => n.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.Property(n => n.ActionPlan).HasColumnName("action_plan");
        builder.Property(n => n.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(n => n.ResolvedBy).HasColumnName("resolved_by");

        builder.Property(n => n.CreatedBy).HasColumnName("created_by");
        builder.Property(n => n.UpdatedBy).HasColumnName("updated_by");
        builder.Property(n => n.DeletedBy).HasColumnName("deleted_by");
        builder.Property(n => n.CreatedAt).HasColumnName("created_at");
        builder.Property(n => n.UpdatedAt).HasColumnName("updated_at");
        builder.Property(n => n.DeletedAt).HasColumnName("deleted_at");

        builder.HasOne(n => n.Student)
               .WithMany()
               .HasForeignKey(n => n.StudentId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.Period)
               .WithMany()
               .HasForeignKey(n => n.PeriodId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.Resolver)
               .WithMany()
               .HasForeignKey(n => n.ResolvedBy)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasQueryFilter(n => n.DeletedAt == null);
    }
}

