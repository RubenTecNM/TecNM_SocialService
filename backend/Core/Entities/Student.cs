namespace TecNM.Api.Core.Entities;

public sealed class Student : AuditableEntity
{
    public long? UserId { get; set; }
    public long CareerId { get; set; }
    public long? ImportBatchId { get; set; }
    public string ControlNumber { get; set; } = string.Empty;
    public string? Curp { get; set; }
    public string FirstSurname { get; set; } = string.Empty;
    public string? SecondSurname { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? Gender { get; set; }
    public string Shift { get; set; } = "morning";
    public bool HasDisability { get; set; }
    public string? DisabilityDescription { get; set; }
    public string? InstitutionalEmail { get; set; }
    public string? PersonalEmail { get; set; }
    public string? PhoneNumber { get; set; }
    public int CurrentSemester { get; set; } = 1;
    public int CompletedCredits { get; set; }
    public decimal CurriculumProgressPercentage { get; set; }
    public bool IsEnrolled { get; set; } = true;
    public bool HasValidImss { get; set; }
    public string? ImssValidityFileUrl { get; set; }
    public DateTimeOffset? ImssValidatedAt { get; set; }
    public long? ImssValidatedBy { get; set; }

    public User? User { get; set; }
    public Career? Career { get; set; }
    public StudentImportBatch? ImportBatch { get; set; }
    public User? ImssValidator { get; set; }
}

