using TecNM.Api.Modules.SocialService;

namespace TecNM.Api.Modules.Internships;

public sealed record InternshipListItemResponse(
    long Id,
    long StudentId,
    string StudentControlNumber,
    string StudentFullName,
    string CareerCode,
    string PeriodCode,
    string? CompanyName,
    string Status,
    int DocumentProgressPercentage,
    bool IsExpedientComplete,
    string? PresentationLetterFolio,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int PendingReviewsCount,
    DateTimeOffset UpdatedAt
);

public sealed record InternshipStudentSummary(
    long Id,
    string ControlNumber,
    string FullName,
    string CareerName,
    int CurrentSemester,
    string Shift,
    string? PhoneNumber,
    string? InstitutionalEmail,
    bool HasValidImss,
    bool HasImssFile
);

public sealed record InternshipPeriodSummary(
    long Id,
    string Code
);

public sealed record InternshipCompanySummary(
    long Id,
    string Name,
    string Sector
);

public sealed record InternshipSupervisorSummary(
    long Id,
    string FullName,
    string Position
);

public sealed record InternshipAgreementSummary(
    long Id,
    string AgreementNumber,
    string Validity
);

public sealed record InternshipEvaluationResponse(
    long Id,
    string ActivityName,
    DateOnly EvaluationStartDate,
    DateOnly EvaluationEndDate,
    IReadOnlyList<int> Criteria,
    decimal NumericalAverage,
    string PerformanceLevel,
    bool IsCompetencyAchieved,
    long? EvaluatorSupervisorId,
    DateOnly EvaluatorSignatureDate,
    string? Observations
);

public sealed record InternshipDetailResponse(
    long Id,
    InternshipStudentSummary Student,
    InternshipPeriodSummary Period,
    InternshipCompanySummary? Company,
    InternshipSupervisorSummary? Supervisor,
    InternshipAgreementSummary? Agreement,
    int SemesterEnrolled,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? ScheduleDescription,
    int RequiredHours,
    int CompletedHours,
    int DocumentProgressPercentage,
    string Status,
    int CreditsAwarded,
    string? PresentationLetterFolio,
    DateOnly? PresentationLetterDate,
    DateOnly? AcceptanceLetterDate,
    DateOnly? TerminationLetterDate,
    bool OriginalTerminationLetterReceived,
    string? ReleaseCertificateFolio,
    DateOnly? ReleaseDate,
    DateTimeOffset? ReleasedAt,
    bool IsExpedientComplete,
    bool SchoolServicesActSent,
    DateTimeOffset? SchoolServicesActSentAt,
    string? SchoolServicesActFolio,
    string? Observations,
    string? CancellationReason,
    InternshipEvaluationResponse? Evaluation,
    IReadOnlyList<string> AllowedActions,
    IReadOnlyList<TramiteChecklistItem> Checklist,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public sealed record CreateInternshipRequest(
    long? StudentId,
    long PeriodId,
    int CurrentSemester,
    string Shift,
    string PhoneNumber,
    string InstitutionalEmail,
    long? CompanyId = null,
    long? SupervisorId = null,
    long? AgreementId = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    string? ScheduleDescription = null,
    int? RequiredHours = null
);

public sealed record ApproveInternshipRequest(
    string? Observations
);

public sealed record InternshipPresentationLetterRequest(
    DateOnly PresentationLetterDate
);

public sealed record RegisterInternshipAcceptanceRequest(
    long CompanyId,
    long SupervisorId,
    long? AgreementId,
    DateOnly StartDate,
    DateOnly EndDate,
    string? ScheduleDescription,
    int? RequiredHours,
    DateOnly AcceptanceLetterDate
);

public sealed record InternshipEvaluationRequest(
    DateOnly EvaluationStartDate,
    DateOnly EvaluationEndDate,
    IReadOnlyList<int> Criteria,
    long? EvaluatorSupervisorId,
    DateOnly EvaluatorSignatureDate,
    string? Observations
);

public sealed record UpdateInternshipExpedientRequest(
    bool IsExpedientComplete,
    bool OriginalTerminationLetterReceived,
    string? Observations
);

public sealed record CompleteInternshipRequest(
    DateOnly TerminationLetterDate
);

public sealed record ReleaseInternshipRequest(
    DateOnly? ReleaseDate
);

