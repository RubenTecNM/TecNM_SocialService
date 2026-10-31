using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TecNM.Api.Core.Entities;

namespace TecNM.Api.Data.Configurations;

public class InternshipConfiguration : IEntityTypeConfiguration<Internship>
{
    public void Configure(EntityTypeBuilder<Internship> builder)
    {
        builder.ToTable("internships");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id");

        builder.Property(i => i.StudentId).HasColumnName("student_id").IsRequired();
        builder.Property(i => i.PeriodId).HasColumnName("period_id").IsRequired();
        builder.Property(i => i.CompanyId).HasColumnName("company_id");
        builder.Property(i => i.SupervisorId).HasColumnName("supervisor_id");
        builder.Property(i => i.AgreementId).HasColumnName("agreement_id");
        builder.Property(i => i.SemesterEnrolled).HasColumnName("semester_enrolled").IsRequired();
        builder.Property(i => i.StartDate).HasColumnName("start_date");
        builder.Property(i => i.EndDate).HasColumnName("end_date");
        builder.Property(i => i.ScheduleDescription).HasColumnName("schedule_description").HasMaxLength(150);
        builder.Property(i => i.RequiredHours).HasColumnName("required_hours").IsRequired();
        builder.Property(i => i.CompletedHours).HasColumnName("completed_hours").IsRequired();
        builder.Property(i => i.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.Property(i => i.CreditsAwarded).HasColumnName("credits_awarded").IsRequired();
        builder.Property(i => i.PresentationLetterFolio).HasColumnName("presentation_letter_folio").HasMaxLength(64);
        builder.Property(i => i.PresentationLetterDate).HasColumnName("presentation_letter_date");
        builder.Property(i => i.AcceptanceLetterDate).HasColumnName("acceptance_letter_date");
        builder.Property(i => i.TerminationLetterDate).HasColumnName("termination_letter_date");
        builder.Property(i => i.OriginalTerminationLetterReceived).HasColumnName("original_termination_letter_received").IsRequired();
        builder.Property(i => i.ReleaseCertificateFolio).HasColumnName("release_certificate_folio").HasMaxLength(64);
        builder.Property(i => i.ReleaseDate).HasColumnName("release_date");
        builder.Property(i => i.ReleasedAt).HasColumnName("released_at");
        builder.Property(i => i.IsExpedientComplete).HasColumnName("is_expedient_complete").IsRequired();
        builder.Property(i => i.SchoolServicesActSent).HasColumnName("school_services_act_sent").IsRequired();
        builder.Property(i => i.SchoolServicesActSentAt).HasColumnName("school_services_act_sent_at");
        builder.Property(i => i.SchoolServicesActFolio).HasColumnName("school_services_act_folio").HasMaxLength(64);
        builder.Property(i => i.Observations).HasColumnName("observations");
        builder.Property(i => i.CancellationReason).HasColumnName("cancellation_reason");

        builder.Property(i => i.CreatedBy).HasColumnName("created_by");
        builder.Property(i => i.UpdatedBy).HasColumnName("updated_by");
        builder.Property(i => i.DeletedBy).HasColumnName("deleted_by");
        builder.Property(i => i.CreatedAt).HasColumnName("created_at");
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at");
        builder.Property(i => i.DeletedAt).HasColumnName("deleted_at");

        builder.HasOne(i => i.Student)
               .WithMany()
               .HasForeignKey(i => i.StudentId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Period)
               .WithMany()
               .HasForeignKey(i => i.PeriodId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Company)
               .WithMany()
               .HasForeignKey(i => i.CompanyId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Supervisor)
               .WithMany()
               .HasForeignKey(i => i.SupervisorId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Agreement)
               .WithMany()
               .HasForeignKey(i => i.AgreementId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(i => i.Evaluation)
               .WithOne(e => e.Internship)
               .HasForeignKey<InternshipEvaluation>(e => e.InternshipId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(i => i.DeletedAt == null);
    }
}

public class InternshipEvaluationConfiguration : IEntityTypeConfiguration<InternshipEvaluation>
{
    public void Configure(EntityTypeBuilder<InternshipEvaluation> builder)
    {
        builder.ToTable("internship_evaluations");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.InternshipId).HasColumnName("internship_id").IsRequired();
        builder.Property(e => e.ActivityName).HasColumnName("activity_name").HasMaxLength(100).IsRequired();
        builder.Property(e => e.EvaluationStartDate).HasColumnName("evaluation_start_date").IsRequired();
        builder.Property(e => e.EvaluationEndDate).HasColumnName("evaluation_end_date").IsRequired();
        builder.Property(e => e.Criterion1).HasColumnName("criterion_1").IsRequired();
        builder.Property(e => e.Criterion2).HasColumnName("criterion_2").IsRequired();
        builder.Property(e => e.Criterion3).HasColumnName("criterion_3").IsRequired();
        builder.Property(e => e.Criterion4).HasColumnName("criterion_4").IsRequired();
        builder.Property(e => e.Criterion5).HasColumnName("criterion_5").IsRequired();
        builder.Property(e => e.Criterion6).HasColumnName("criterion_6").IsRequired();
        builder.Property(e => e.Criterion7).HasColumnName("criterion_7").IsRequired();
        builder.Property(e => e.NumericalAverage).HasColumnName("numerical_average").HasPrecision(3, 2).IsRequired();
        builder.Property(e => e.PerformanceLevel).HasColumnName("performance_level").HasMaxLength(32).IsRequired();
        builder.Property(e => e.IsCompetencyAchieved).HasColumnName("is_competency_achieved").IsRequired();
        builder.Property(e => e.EvaluatorSupervisorId).HasColumnName("evaluator_supervisor_id");
        builder.Property(e => e.EvaluatorSignatureDate).HasColumnName("evaluator_signature_date").IsRequired();
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

