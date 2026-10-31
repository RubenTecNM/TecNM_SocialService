namespace TecNM.Api.Core.Entities;

public class Internship : AuditableEntity
{
    public long StudentId { get; set; }
    public long PeriodId { get; set; }
    public long? CompanyId { get; set; }
    public long? SupervisorId { get; set; }
    public long? AgreementId { get; set; }
    public int SemesterEnrolled { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? ScheduleDescription { get; set; }
    public int RequiredHours { get; set; } = 200;
    public int CompletedHours { get; set; }
    public string Status { get; set; } = "requested";
    public int CreditsAwarded { get; set; } = 2;
    public string? PresentationLetterFolio { get; set; }
    public DateOnly? PresentationLetterDate { get; set; }
    public DateOnly? AcceptanceLetterDate { get; set; }
    public DateOnly? TerminationLetterDate { get; set; }
    public bool OriginalTerminationLetterReceived { get; set; }
    public string? ReleaseCertificateFolio { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
    public bool IsExpedientComplete { get; set; }
    public bool SchoolServicesActSent { get; set; }
    public DateTimeOffset? SchoolServicesActSentAt { get; set; }
    public string? SchoolServicesActFolio { get; set; }
    public string? Observations { get; set; }
    public string? CancellationReason { get; set; }

    public Student? Student { get; set; }
    public AcademicPeriod? Period { get; set; }
    public Company? Company { get; set; }
    public CompanySupervisor? Supervisor { get; set; }
    public Agreement? Agreement { get; set; }
    public InternshipEvaluation? Evaluation { get; set; }
}

public class InternshipEvaluation : AuditableEntity
{
    public long InternshipId { get; set; }
    public string ActivityName { get; set; } = "PRODUCTIVIDAD LABORAL";
    public DateOnly EvaluationStartDate { get; set; }
    public DateOnly EvaluationEndDate { get; set; }
    public int Criterion1 { get; set; } = 4;
    public int Criterion2 { get; set; } = 4;
    public int Criterion3 { get; set; } = 4;
    public int Criterion4 { get; set; } = 4;
    public int Criterion5 { get; set; } = 4;
    public int Criterion6 { get; set; } = 4;
    public int Criterion7 { get; set; } = 4;
    public decimal NumericalAverage { get; set; } = 4.00m;
    public string PerformanceLevel { get; set; } = "excellent";
    public bool IsCompetencyAchieved { get; set; } = true;
    public long? EvaluatorSupervisorId { get; set; }
    public DateOnly EvaluatorSignatureDate { get; set; }
    public string? Observations { get; set; }

    public Internship? Internship { get; set; }
    public CompanySupervisor? EvaluatorSupervisor { get; set; }
}

