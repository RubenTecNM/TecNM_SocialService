namespace TecNM.Api.Core.Entities;

public class SocialServiceApplication : AuditableEntity
{
    public long SocialServiceId { get; set; }
    public string StudentAddress { get; set; } = string.Empty;
    public string StudentPhone { get; set; } = string.Empty;
    public string PeriodType { get; set; } = "february_july";
    public int CurrentSemester { get; set; }
    public string DependencyName { get; set; } = string.Empty;
    public string DependencyHeadName { get; set; } = string.Empty;
    public string DependencyHeadPosition { get; set; } = string.Empty;
    public string ActivitiesOutline { get; set; } = string.Empty;
    public bool? IsAcceptedByOffice { get; set; }
    public string? RejectionReason { get; set; }
    public string? OfficeObservations { get; set; }
    public long? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }

    public SocialService? SocialService { get; set; }
    public User? Reviewer { get; set; }
}

public class SocialServiceCommitment : AuditableEntity
{
    public long SocialServiceId { get; set; }
    public string StudentControlNumber { get; set; } = string.Empty;
    public string StudentAddress { get; set; } = string.Empty;
    public string StudentPhone { get; set; } = string.Empty;
    public string DependencyAddress { get; set; } = string.Empty;
    public DateOnly CommitmentDate { get; set; }
    public string City { get; set; } = "Monclova";
    public bool IsSignedByStudent { get; set; } = true;
    public DateTimeOffset SignedAt { get; set; } = DateTimeOffset.UtcNow;

    public SocialService? SocialService { get; set; }
}

public class SocialServiceBiMonthlyReport : AuditableEntity
{
    public long SocialServiceId { get; set; }
    public int ReportNumber { get; set; }
    public DateOnly PeriodStartDate { get; set; }
    public DateOnly PeriodEndDate { get; set; }
    public string ActivitiesSummary { get; set; } = string.Empty;
    public int ReportedHours { get; set; }
    public int AccumulatedHours { get; set; }
    public long? EvaluatorSupervisorId { get; set; }
    public string Status { get; set; } = "submitted";
    public string? OfficeObservations { get; set; }
    public long? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }

    public SocialService? SocialService { get; set; }
    public CompanySupervisor? EvaluatorSupervisor { get; set; }
    public User? Approver { get; set; }
}

public class SocialServiceEvaluation : AuditableEntity
{
    public long SocialServiceId { get; set; }
    public string EvaluatorType { get; set; } = "external_supervisor";
    public string BimonthlyPeriod { get; set; } = "final";
    public string Criterion1 { get; set; } = "excellent";
    public string Criterion2 { get; set; } = "excellent";
    public string Criterion3 { get; set; } = "excellent";
    public string Criterion4 { get; set; } = "excellent";
    public string Criterion5 { get; set; } = "excellent";
    public string Criterion6 { get; set; } = "excellent";
    public string Criterion7 { get; set; } = "excellent";
    public long? EvaluatorSupervisorId { get; set; }
    public string? Observations { get; set; }

    public SocialService? SocialService { get; set; }
    public CompanySupervisor? EvaluatorSupervisor { get; set; }
}

public class SocialServiceActivityAssessment : AuditableEntity
{
    public long SocialServiceId { get; set; }
    public string Question1 { get; set; } = "excellent";
    public string Question2 { get; set; } = "excellent";
    public string Question3 { get; set; } = "excellent";
    public string Question4 { get; set; } = "excellent";
    public string Question5 { get; set; } = "excellent";
    public string Question6 { get; set; } = "excellent";
    public string Question7 { get; set; } = "excellent";
    public string Question8 { get; set; } = "excellent";
    public string? Observations { get; set; }

    public SocialService? SocialService { get; set; }
}

