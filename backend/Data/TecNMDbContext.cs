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
    public DbSet<SocialServiceProgram> SocialServicePrograms => Set<SocialServiceProgram>();
    public DbSet<InstitutionalAuthority> InstitutionalAuthorities => Set<InstitutionalAuthority>();
    public DbSet<DocumentSeries> DocumentSeries => Set<DocumentSeries>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanySupervisor> CompanySupervisors => Set<CompanySupervisor>();
    public DbSet<Agreement> Agreements => Set<Agreement>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<StudentImportBatch> StudentImportBatches => Set<StudentImportBatch>();
    public DbSet<ExpedientDocument> ExpedientDocuments => Set<ExpedientDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TecNMDbContext).Assembly);
    }
}
