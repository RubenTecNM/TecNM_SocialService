using Microsoft.EntityFrameworkCore;
using TecNM.Api.Core.Entities;

namespace TecNM.Api.Data;

public sealed class TecNMDbContext(DbContextOptions<TecNMDbContext> options) : DbContext(options)
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Career> Careers => Set<Career>();
    public DbSet<AcademicPeriod> AcademicPeriods => Set<AcademicPeriod>();
    public DbSet<ActivityType> ActivityTypes => Set<ActivityType>();
    public DbSet<SocialServiceProgram> SocialServicePrograms => Set<SocialServiceProgram>();
    public DbSet<InstitutionalAuthority> InstitutionalAuthorities => Set<InstitutionalAuthority>();
    public DbSet<DocumentSeries> DocumentSeries => Set<DocumentSeries>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanySupervisor> CompanySupervisors => Set<CompanySupervisor>();
    public DbSet<Agreement> Agreements => Set<Agreement>();
    public DbSet<StudentSocialServiceFollowUp> StudentSocialServiceFollowUps => Set<StudentSocialServiceFollowUp>();
    public DbSet<SocialServiceFollowUpReview> SocialServiceFollowUpReviews => Set<SocialServiceFollowUpReview>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<StudentImportBatch> StudentImportBatches => Set<StudentImportBatch>();
    public DbSet<ExpedientDocument> ExpedientDocuments => Set<ExpedientDocument>();
    public DbSet<SocialService> SocialServices => Set<SocialService>();
    public DbSet<SocialServiceApplication> SocialServiceApplications => Set<SocialServiceApplication>();
    public DbSet<SocialServiceCommitment> SocialServiceCommitments => Set<SocialServiceCommitment>();
    public DbSet<SocialServiceBiMonthlyReport> SocialServiceBiMonthlyReports => Set<SocialServiceBiMonthlyReport>();
    public DbSet<SocialServiceEvaluation> SocialServiceEvaluations => Set<SocialServiceEvaluation>();
    public DbSet<SocialServiceActivityAssessment> SocialServiceActivityAssessments => Set<SocialServiceActivityAssessment>();
    public DbSet<Internship> Internships => Set<Internship>();
    public DbSet<InternshipEvaluation> InternshipEvaluations => Set<InternshipEvaluation>();
    public DbSet<TramiteStatusLog> TramiteStatusLogs => Set<TramiteStatusLog>();
    public DbSet<NonConformance> NonConformances => Set<NonConformance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        TecNM.Api.Modules.Expedients.Templates.DocumentTemplate.Configure(modelBuilder);
        modelBuilder.Entity<StudentSocialServiceFollowUp>(b => {
            b.HasNoKey().ToView("vw_student_social_service_follow_up");
            foreach (var property in typeof(StudentSocialServiceFollowUp).GetProperties())
                b.Property(property.Name).HasColumnName(System.Text.RegularExpressions.Regex.Replace(property.Name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant());
        });
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TecNMDbContext).Assembly);
        modelBuilder.Entity<LinkageCompanyMetric>(entity =>
        {
            entity.HasNoKey().ToView("vw_linkage_company_metrics");
            entity.Property(m => m.TramiteType).HasColumnName("tramite_type");
            entity.Property(m => m.PeriodId).HasColumnName("period_id");
            entity.Property(m => m.CompanyId).HasColumnName("company_id");
            entity.Property(m => m.CompanyName).HasColumnName("company_name");
            entity.Property(m => m.CompanyType).HasColumnName("company_type");
            entity.Property(m => m.Projects).HasColumnName("projects");
            entity.Property(m => m.AssignedStudents).HasColumnName("assigned_students");
            entity.Property(m => m.ActiveStudents).HasColumnName("active_students");
        });
        modelBuilder.Entity<LinkageTypeMetric>(entity =>
        {
            entity.HasNoKey().ToView("vw_linkage_company_type_metrics");
            entity.Property(m => m.TramiteType).HasColumnName("tramite_type");
            entity.Property(m => m.PeriodId).HasColumnName("period_id");
            entity.Property(m => m.CompanyType).HasColumnName("company_type");
            entity.Property(m => m.Projects).HasColumnName("projects");
            entity.Property(m => m.AssignedStudents).HasColumnName("assigned_students");
            entity.Property(m => m.ActiveStudents).HasColumnName("active_students");
        });

    }
}
