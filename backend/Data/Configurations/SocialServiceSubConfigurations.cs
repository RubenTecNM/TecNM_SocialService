using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TecNM.Api.Core.Entities;

namespace TecNM.Api.Data.Configurations;

public class SocialServiceApplicationConfiguration : IEntityTypeConfiguration<SocialServiceApplication>
{
    public void Configure(EntityTypeBuilder<SocialServiceApplication> builder)
    {
        builder.ToTable("social_service_applications");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.SocialServiceId).HasColumnName("social_service_id").IsRequired();
        builder.Property(a => a.StudentAddress).HasColumnName("student_address").IsRequired();
        builder.Property(a => a.StudentPhone).HasColumnName("student_phone").HasMaxLength(20).IsRequired();
        builder.Property(a => a.PeriodType).HasColumnName("period_type").HasMaxLength(20).IsRequired();
        builder.Property(a => a.CurrentSemester).HasColumnName("current_semester").IsRequired();
        builder.Property(a => a.DependencyName).HasColumnName("dependency_name").HasMaxLength(200).IsRequired();
        builder.Property(a => a.DependencyHeadName).HasColumnName("dependency_head_name").HasMaxLength(180).IsRequired();
        builder.Property(a => a.DependencyHeadPosition).HasColumnName("dependency_head_position").HasMaxLength(150).IsRequired();
        builder.Property(a => a.ActivitiesOutline).HasColumnName("activities_outline").IsRequired();
        builder.Property(a => a.IsAcceptedByOffice).HasColumnName("is_accepted_by_office");
        builder.Property(a => a.RejectionReason).HasColumnName("rejection_reason");
        builder.Property(a => a.OfficeObservations).HasColumnName("office_observations");
        builder.Property(a => a.ReviewedBy).HasColumnName("reviewed_by");
        builder.Property(a => a.ReviewedAt).HasColumnName("reviewed_at");

        builder.Property(a => a.CreatedBy).HasColumnName("created_by");
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by");
        builder.Property(a => a.DeletedBy).HasColumnName("deleted_by");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.DeletedAt).HasColumnName("deleted_at");

        builder.HasOne(a => a.Reviewer)
               .WithMany()
               .HasForeignKey(a => a.ReviewedBy)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasQueryFilter(a => a.DeletedAt == null);
    }
}

public class SocialServiceCommitmentConfiguration : IEntityTypeConfiguration<SocialServiceCommitment>
{
    public void Configure(EntityTypeBuilder<SocialServiceCommitment> builder)
    {
        builder.ToTable("social_service_commitments");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.SocialServiceId).HasColumnName("social_service_id").IsRequired();
        builder.Property(c => c.StudentControlNumber).HasColumnName("student_control_number").HasMaxLength(32).IsRequired();
        builder.Property(c => c.StudentAddress).HasColumnName("student_address").IsRequired();
        builder.Property(c => c.StudentPhone).HasColumnName("student_phone").HasMaxLength(20).IsRequired();
        builder.Property(c => c.DependencyAddress).HasColumnName("dependency_address").IsRequired();
        builder.Property(c => c.CommitmentDate).HasColumnName("commitment_date").IsRequired();
        builder.Property(c => c.City).HasColumnName("city").HasMaxLength(100).IsRequired();
        builder.Property(c => c.IsSignedByStudent).HasColumnName("is_signed_by_student").IsRequired();
        builder.Property(c => c.SignedAt).HasColumnName("signed_at").IsRequired();

        builder.Property(c => c.CreatedBy).HasColumnName("created_by");
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by");
        builder.Property(c => c.DeletedBy).HasColumnName("deleted_by");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.DeletedAt).HasColumnName("deleted_at");

        builder.HasQueryFilter(c => c.DeletedAt == null);
    }
}

public class SocialServiceBiMonthlyReportConfiguration : IEntityTypeConfiguration<SocialServiceBiMonthlyReport>
{
    public void Configure(EntityTypeBuilder<SocialServiceBiMonthlyReport> builder)
    {
        builder.ToTable("social_service_bi_monthly_reports");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.SocialServiceId).HasColumnName("social_service_id").IsRequired();
        builder.Property(r => r.ReportNumber).HasColumnName("report_number").IsRequired();
        builder.Property(r => r.PeriodStartDate).HasColumnName("period_start_date").IsRequired();
        builder.Property(r => r.PeriodEndDate).HasColumnName("period_end_date").IsRequired();
        builder.Property(r => r.ActivitiesSummary).HasColumnName("activities_summary").IsRequired();
        builder.Property(r => r.ReportedHours).HasColumnName("reported_hours").IsRequired();
        builder.Property(r => r.AccumulatedHours).HasColumnName("accumulated_hours").IsRequired();
        builder.Property(r => r.EvaluatorSupervisorId).HasColumnName("evaluator_supervisor_id");
        builder.Property(r => r.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.Property(r => r.OfficeObservations).HasColumnName("office_observations");
        builder.Property(r => r.ApprovedBy).HasColumnName("approved_by");
        builder.Property(r => r.ApprovedAt).HasColumnName("approved_at");

        builder.Property(r => r.CreatedBy).HasColumnName("created_by");
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by");
        builder.Property(r => r.DeletedBy).HasColumnName("deleted_by");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.DeletedAt).HasColumnName("deleted_at");

        builder.HasOne(r => r.EvaluatorSupervisor)
               .WithMany()
               .HasForeignKey(r => r.EvaluatorSupervisorId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(r => r.Approver)
               .WithMany()
               .HasForeignKey(r => r.ApprovedBy)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasQueryFilter(r => r.DeletedAt == null);
    }
}

public class SocialServiceEvaluationConfiguration : IEntityTypeConfiguration<SocialServiceEvaluation>
{
    public void Configure(EntityTypeBuilder<SocialServiceEvaluation> builder)
    {
        builder.ToTable("social_service_evaluations");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SocialServiceId).HasColumnName("social_service_id").IsRequired();
        builder.Property(e => e.EvaluatorType).HasColumnName("evaluator_type").HasMaxLength(20).IsRequired();
        builder.Property(e => e.BimonthlyPeriod).HasColumnName("bimonthly_period").HasMaxLength(20).IsRequired();
        builder.Property(e => e.Criterion1).HasColumnName("criterion_1").HasMaxLength(16).IsRequired();
        builder.Property(e => e.Criterion2).HasColumnName("criterion_2").HasMaxLength(16).IsRequired();
        builder.Property(e => e.Criterion3).HasColumnName("criterion_3").HasMaxLength(16).IsRequired();
        builder.Property(e => e.Criterion4).HasColumnName("criterion_4").HasMaxLength(16).IsRequired();
        builder.Property(e => e.Criterion5).HasColumnName("criterion_5").HasMaxLength(16).IsRequired();
        builder.Property(e => e.Criterion6).HasColumnName("criterion_6").HasMaxLength(16).IsRequired();
        builder.Property(e => e.Criterion7).HasColumnName("criterion_7").HasMaxLength(16).IsRequired();
        builder.Property(e => e.EvaluatorSupervisorId).HasColumnName("evaluator_supervisor_id");
        builder.Property(e => e.Observations).HasColumnName("observations");

        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        builder.Property(e => e.DeletedBy).HasColumnName("deleted_by");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.DeletedAt).HasColumnName("deleted_at");

        builder.HasOne(e => e.EvaluatorSupervisor)
               .WithMany()
               .HasForeignKey(e => e.EvaluatorSupervisorId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasQueryFilter(e => e.DeletedAt == null);
    }
}

public class SocialServiceActivityAssessmentConfiguration : IEntityTypeConfiguration<SocialServiceActivityAssessment>
{
    public void Configure(EntityTypeBuilder<SocialServiceActivityAssessment> builder)
    {
        builder.ToTable("social_service_activity_assessments");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.SocialServiceId).HasColumnName("social_service_id").IsRequired();
        builder.Property(a => a.Question1).HasColumnName("question_1").HasMaxLength(16).IsRequired();
        builder.Property(a => a.Question2).HasColumnName("question_2").HasMaxLength(16).IsRequired();
        builder.Property(a => a.Question3).HasColumnName("question_3").HasMaxLength(16).IsRequired();
        builder.Property(a => a.Question4).HasColumnName("question_4").HasMaxLength(16).IsRequired();
        builder.Property(a => a.Question5).HasColumnName("question_5").HasMaxLength(16).IsRequired();
        builder.Property(a => a.Question6).HasColumnName("question_6").HasMaxLength(16).IsRequired();
        builder.Property(a => a.Question7).HasColumnName("question_7").HasMaxLength(16).IsRequired();
        builder.Property(a => a.Question8).HasColumnName("question_8").HasMaxLength(16).IsRequired();
        builder.Property(a => a.Observations).HasColumnName("observations");

        builder.Property(a => a.CreatedBy).HasColumnName("created_by");
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by");
        builder.Property(a => a.DeletedBy).HasColumnName("deleted_by");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.DeletedAt).HasColumnName("deleted_at");

        builder.HasQueryFilter(a => a.DeletedAt == null);
    }
}

