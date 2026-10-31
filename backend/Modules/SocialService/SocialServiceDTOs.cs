using TecNM.Api.Core.Http;

namespace TecNM.Api.Modules.SocialService;

public sealed record SocialServiceListItemResponse(
    long Id,
    long StudentId,
    string StudentControlNumber,
    string StudentFullName,
    string CareerCode,
    string PeriodCode,
    string ProgramName,
    string CompanyName,
    string Modality,
    string Status,
    int DocumentProgressPercentage,
    int AccumulatedHours,
    int RequiredHours,
    DateOnly StartDate,
    DateOnly EndDate,
    int PendingReviewsCount,
    DateTimeOffset UpdatedAt
);

public sealed record SocialServiceStudentSummary(
    long Id,
    string ControlNumber,
    string FullName,
    string CareerName,
    bool HasValidImss,
    bool HasImssFile
);

public sealed record SocialServicePeriodSummary(
    long Id,
    string Code
);

public sealed record SocialServiceProgramSummary(
    long Id,
    string Code,
    string Name,
    string Category
);

public sealed record SocialServiceCompanySummary(
    long Id,
    string Name,
    bool IsTecnmInternal
);

public sealed record SocialServiceSupervisorSummary(
    long Id,
    string FullName,
    string Position
);

public sealed record SocialServiceAgreementSummary(
    long Id,
    string AgreementNumber,
    string Validity
);

public sealed record SocialServiceApplicationResponse(
    string StudentAddress,
    string StudentPhone,
    string PeriodType,
    int CurrentSemester,
    string DependencyName,
    string DependencyHeadName,
    string DependencyHeadPosition,
    string ActivitiesOutline,
    bool? IsAcceptedByOffice,
    string? RejectionReason,
    string? OfficeObservations,
    string? ReviewedByName,
    DateTimeOffset? ReviewedAt
);

public sealed record SocialServiceCommitmentResponse(
    string StudentControlNumber,
    string StudentAddress,
    string StudentPhone,
    string DependencyAddress,
    DateOnly CommitmentDate,
    string City,
    bool IsSignedByStudent,
    DateTimeOffset SignedAt
);

public sealed record BiMonthlyReportResponse(
    long Id,
    int ReportNumber,
    DateOnly PeriodStartDate,
    DateOnly PeriodEndDate,
    string ActivitiesSummary,
    int ReportedHours,
    int AccumulatedHours,
    long? EvaluatorSupervisorId,
    string Status,
    string? OfficeObservations,
    string? ApprovedByName,
    DateTimeOffset? ApprovedAt
);

public sealed record SocialServiceEvaluationResponse(
    long Id,
    string EvaluatorType,
    string BimonthlyPeriod,
    IReadOnlyList<string> Criteria,
    long? EvaluatorSupervisorId,
    string? Observations
);

public sealed record ActivityAssessmentResponse(
    long Id,
    IReadOnlyList<string> Questions,
    string? Observations,
    DateTimeOffset CreatedAt
);

public sealed record TramiteChecklistItem(
    string Key,
    string Label,
    bool Done,
    string? Detail
);

public sealed record StatusLogItemResponse(
    long Id,
    string? PreviousStatus,
    string NewStatus,
    string? Reason,
    string ChangedByName,
    DateTimeOffset CreatedAt
);

public sealed record SocialServiceDetailResponse(
    long Id,
    SocialServiceStudentSummary Student,
    SocialServicePeriodSummary Period,
    SocialServiceProgramSummary Program,
    SocialServiceCompanySummary Company,
    SocialServiceSupervisorSummary Supervisor,
    SocialServiceAgreementSummary? Agreement,
    int SemesterEnrolled,
    string Modality,
    DateOnly StartDate,
    DateOnly EndDate,
    int RequiredHours,
    int AccumulatedHours,
    int DocumentProgressPercentage,
    string Status,
    string? PresentationLetterFolio,
    DateOnly? PresentationLetterDate,
    DateOnly? AcceptanceLetterDate,
    DateOnly? TerminationLetterDate,
    bool OriginalTerminationLetterReceived,
    string? ReleaseCertificateFolio,
    DateTimeOffset? ReleasedAt,
    string? FinalGradeLevel,
    decimal? FinalScore,
    bool SchoolServicesActSent,
    DateTimeOffset? SchoolServicesActSentAt,
    string? SchoolServicesActFolio,
    int BeneficiariesCount,
    bool HasInclusionActivities,
    string? TargetCommunity,
    string? Observations,
    string? CancellationReason,
    SocialServiceApplicationResponse Application,
    SocialServiceCommitmentResponse? Commitment,
    IReadOnlyList<BiMonthlyReportResponse> Reports,
    IReadOnlyList<SocialServiceEvaluationResponse> Evaluations,
    ActivityAssessmentResponse? ActivityAssessment,
    IReadOnlyList<string> AllowedActions,
    IReadOnlyList<TramiteChecklistItem> Checklist,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public sealed record SocialServiceApplicationInput(
    string StudentAddress,
    string StudentPhone,
    string PeriodType,
    string DependencyName,
    string DependencyHeadName,
    string DependencyHeadPosition,
    string ActivitiesOutline
);

public sealed record CreateSocialServiceRequest(
    long? StudentId,
    long PeriodId,
    long ProgramId,
    long CompanyId,
    long SupervisorId,
    long? AgreementId,
    string Modality,
    DateOnly StartDate,
    DateOnly EndDate,
    string? TargetCommunity,
    SocialServiceApplicationInput Application
);

public sealed record UpdateSocialServiceRequest(
    long PeriodId,
    long ProgramId,
    long CompanyId,
    long SupervisorId,
    long? AgreementId,
    string Modality,
    DateOnly StartDate,
    DateOnly EndDate,
    string? TargetCommunity,
    SocialServiceApplicationInput Application
);

public sealed record ReviewApplicationRequest(
    bool IsAccepted,
    string? RejectionReason,
    string? OfficeObservations
);

public sealed record PresentationLetterRequest(
    DateOnly PresentationLetterDate
);

public sealed record AcceptanceLetterRequest(
    DateOnly AcceptanceLetterDate
);

public sealed record CommitmentRequest(
    string StudentAddress,
    string StudentPhone,
    string DependencyAddress,
    DateOnly CommitmentDate,
    string? City
);

public sealed record ReportRequest(
    DateOnly PeriodStartDate,
    DateOnly PeriodEndDate,
    string ActivitiesSummary,
    int ReportedHours,
    long? EvaluatorSupervisorId
);

public sealed record ReviewReportRequest(
    string Status,
    string? OfficeObservations
);

public sealed record EvaluationRequest(
    IReadOnlyList<string> Criteria,
    long? EvaluatorSupervisorId,
    string? Observations
);

public sealed record ActivityAssessmentRequest(
    IReadOnlyList<string> Questions,
    string? Observations
);

public sealed record UpdateImpactRequest(
    int BeneficiariesCount,
    bool HasInclusionActivities,
    string? TargetCommunity,
    string? Observations
);

public sealed record CompleteSocialServiceRequest(
    DateOnly TerminationLetterDate
);

public sealed record ReleaseSocialServiceRequest(
    string? FinalGradeLevel,
    decimal? FinalScore,
    bool OriginalTerminationLetterReceived = true
);

public sealed record CancelTramiteRequest(
    string Reason
);

public sealed record MarkNonComplianceRequest(
    string Category,
    string Description
);

