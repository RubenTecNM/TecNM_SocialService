using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TecNM.Api.Core.Entities;

namespace TecNM.Api.Data.Configurations;

public class SocialServiceConfiguration : IEntityTypeConfiguration<SocialService>
{
    public void Configure(EntityTypeBuilder<SocialService> builder)
    {
        builder.ToTable("social_services");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        builder.Property(s => s.StudentId).HasColumnName("student_id").IsRequired();
        builder.Property(s => s.PeriodId).HasColumnName("period_id").IsRequired();
        builder.Property(s => s.ProgramId).HasColumnName("program_id").IsRequired();
        builder.Property(s => s.AgreementId).HasColumnName("agreement_id");
        builder.Property(s => s.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(s => s.SupervisorId).HasColumnName("supervisor_id").IsRequired();
        builder.Property(s => s.SemesterEnrolled).HasColumnName("semester_enrolled").IsRequired();
        builder.Property(s => s.Modality).HasColumnName("modality").HasMaxLength(20).IsRequired();
        builder.Property(s => s.StartDate).HasColumnName("start_date").IsRequired();
        builder.Property(s => s.EndDate).HasColumnName("end_date").IsRequired();
        builder.Property(s => s.RequiredHours).HasColumnName("required_hours").IsRequired();
        builder.Property(s => s.AccumulatedHours).HasColumnName("accumulated_hours").IsRequired();
        builder.Property(s => s.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.Property(s => s.PresentationLetterFolio).HasColumnName("presentation_letter_folio").HasMaxLength(64);
        builder.Property(s => s.PresentationLetterDate).HasColumnName("presentation_letter_date");
        builder.Property(s => s.AcceptanceLetterDate).HasColumnName("acceptance_letter_date");
        builder.Property(s => s.TerminationLetterDate).HasColumnName("termination_letter_date");
        builder.Property(s => s.OriginalTerminationLetterReceived).HasColumnName("original_termination_letter_received").IsRequired();
        builder.Property(s => s.ReleaseCertificateFolio).HasColumnName("release_certificate_folio").HasMaxLength(64);
        builder.Property(s => s.ReleasedAt).HasColumnName("released_at");
        builder.Property(s => s.FinalGradeLevel).HasColumnName("final_grade_level").HasMaxLength(32);
        builder.Property(s => s.FinalScore).HasColumnName("final_score").HasPrecision(5, 2);
        builder.Property(s => s.SchoolServicesActSent).HasColumnName("school_services_act_sent").IsRequired();
        builder.Property(s => s.SchoolServicesActSentAt).HasColumnName("school_services_act_sent_at");
        builder.Property(s => s.SchoolServicesActFolio).HasColumnName("school_services_act_folio").HasMaxLength(64);
        builder.Property(s => s.BeneficiariesCount).HasColumnName("beneficiaries_count").IsRequired();
        builder.Property(s => s.HasInclusionActivities).HasColumnName("has_inclusion_activities").IsRequired();
        builder.Property(s => s.TargetCommunity).HasColumnName("target_community").HasMaxLength(200);
        builder.Property(s => s.Observations).HasColumnName("observations");
        builder.Property(s => s.CancellationReason).HasColumnName("cancellation_reason");

        builder.Property(s => s.CreatedBy).HasColumnName("created_by");
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by");
        builder.Property(s => s.DeletedBy).HasColumnName("deleted_by");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.DeletedAt).HasColumnName("deleted_at");

        builder.HasOne(s => s.Student)
               .WithMany()
               .HasForeignKey(s => s.StudentId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Period)
               .WithMany()
               .HasForeignKey(s => s.PeriodId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Program)
               .WithMany()
               .HasForeignKey(s => s.ProgramId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Agreement)
               .WithMany()
               .HasForeignKey(s => s.AgreementId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(s => s.Company)
               .WithMany()
               .HasForeignKey(s => s.CompanyId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Supervisor)
               .WithMany()
               .HasForeignKey(s => s.SupervisorId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Application)
               .WithOne(a => a.SocialService)
               .HasForeignKey<SocialServiceApplication>(a => a.SocialServiceId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Commitment)
               .WithOne(c => c.SocialService)
               .HasForeignKey<SocialServiceCommitment>(c => c.SocialServiceId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Reports)
               .WithOne(r => r.SocialService)
               .HasForeignKey(r => r.SocialServiceId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Evaluations)
               .WithOne(e => e.SocialService)
               .HasForeignKey(e => e.SocialServiceId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.ActivityAssessment)
               .WithOne(a => a.SocialService)
               .HasForeignKey<SocialServiceActivityAssessment>(a => a.SocialServiceId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(s => s.DeletedAt == null);
    }
}

