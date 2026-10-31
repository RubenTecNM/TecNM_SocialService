namespace TecNM.Api.Core.Entities;

public class SocialService : AuditableEntity
{
    public long StudentId { get; set; }
    public long PeriodId { get; set; }
    public long ProgramId { get; set; }
    public long? AgreementId { get; set; }
    public long CompanyId { get; set; }
    public long SupervisorId { get; set; }
    public int SemesterEnrolled { get; set; }
    public string Modality { get; set; } = "external";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int RequiredHours { get; set; } = 480;
    public int AccumulatedHours { get; set; }
    public string Status { get; set; } = "requested";
    public string? PresentationLetterFolio { get; set; }
    public DateOnly? PresentationLetterDate { get; set; }
    public DateOnly? AcceptanceLetterDate { get; set; }
    public DateOnly? TerminationLetterDate { get; set; }
    public bool OriginalTerminationLetterReceived { get; set; }
    public string? ReleaseCertificateFolio { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
    public string? FinalGradeLevel { get; set; }
    public decimal? FinalScore { get; set; }
    public bool SchoolServicesActSent { get; set; }
    public DateTimeOffset? SchoolServicesActSentAt { get; set; }
    public string? SchoolServicesActFolio { get; set; }
    public int BeneficiariesCount { get; set; }
    public bool HasInclusionActivities { get; set; }
    public string? TargetCommunity { get; set; }
    public string? Observations { get; set; }
    public string? CancellationReason { get; set; }

    public Student? Student { get; set; }
    public AcademicPeriod? Period { get; set; }
    public SocialServiceProgram? Program { get; set; }
    public Agreement? Agreement { get; set; }
    public Company? Company { get; set; }
    public CompanySupervisor? Supervisor { get; set; }

    public SocialServiceApplication? Application { get; set; }
    public SocialServiceCommitment? Commitment { get; set; }
    public ICollection<SocialServiceBiMonthlyReport> Reports { get; set; } = new List<SocialServiceBiMonthlyReport>();
    public ICollection<SocialServiceEvaluation> Evaluations { get; set; } = new List<SocialServiceEvaluation>();
    public SocialServiceActivityAssessment? ActivityAssessment { get; set; }
}

