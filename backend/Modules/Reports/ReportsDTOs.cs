namespace TecNM.Api.Modules.Reports;

public sealed record ReportPeriodSummary(
    long Id,
    string Code,
    string Name
);

public sealed record DashboardResponse(
    ReportPeriodSummary Period,
    IReadOnlyDictionary<string, int> SocialService,
    IReadOnlyDictionary<string, int> Internships,
    int PendingDocumentReviews,
    int AgreementsExpiringSoon,
    int OpenNonConformances
);

public sealed record TrackingDocumentsChecklist(
    bool Cp,
    bool Ca,
    bool? Sol,
    bool? Cc,
    bool? R1,
    bool? R2,
    bool? R3,
    bool? Ev1,
    bool? Ev2,
    bool? Ev3,
    bool? Ev,
    bool Ct,
    bool? Qualitative = null,
    bool? SelfAssessment = null,
    bool? ActivityAssessment = null
);

public sealed record TrackingRowResponse(
    long TramiteId,
    string ControlNumber,
    string StudentFullName,
    string CareerCode,
    string CareerName,
    int SemesterEnrolled,
    string? Gender,
    string Shift,
    string? PhoneNumber,
    string? CompanyName,
    string? CompanySector,
    string? SupervisorName,
    string? AgreementNumber,
    string? ProgramName,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string Status,
    int ProgressPercentage,
    TrackingDocumentsChecklist Documents,
    string? PresentationLetterFolio,
    string? ReleaseCertificateFolio,
    bool OriginalTerminationLetterReceived,
    string? FinalGradeLevel,
    bool IsPnc,
    string? Observations,
    string? SchoolServicesActFolio,
    int? BeneficiariesCount,
    bool? HasInclusionActivities,
    string? TargetCommunity,
    string? PeriodCode = null,
    string? SchoolYear = null,
    string? ActivityTypeName = null
);

public sealed record CareerStatisticalBreakdown(
    string CareerCode,
    string CareerName,
    int EligibleWithPresentationLetter,
    int ApplicantsMale,
    int ApplicantsFemale,
    int ApplicantsTotal,
    int ReleasedMale,
    int ReleasedFemale,
    int ReleasedTotal,
    int PncTotal,
    decimal ReleaseRatePercentage
);

public sealed record StatisticalTotals(
    int EligibleWithPresentationLetter,
    int ApplicantsMale,
    int ApplicantsFemale,
    int ApplicantsTotal,
    int ReleasedMale,
    int ReleasedFemale,
    int ReleasedTotal,
    int PncTotal,
    decimal ReleaseRatePercentage
);

public sealed record SchoolYearComparison(
    string FirstPeriodCode,
    string SecondPeriodCode,
    int TotalEligible,
    int TotalApplicants,
    int TotalReleased,
    int TotalPnc,
    decimal SchoolYearReleaseRatePercentage
);

public sealed record StatisticalReportResponse(
    ReportPeriodSummary Period,
    string TramiteType,
    IReadOnlyList<CareerStatisticalBreakdown> ByCareer,
    StatisticalTotals Totals,
    SchoolYearComparison? SchoolYearComparison
);

public sealed record Report911CareerGenderRow(
    string CareerCode,
    string Sector,
    int Male,
    int Female,
    int Unspecified,
    int Total,
    int WithDisability
);

public sealed record Report911SectorRow(
    string Sector,
    int Male,
    int Female,
    int Total,
    int WithDisability
);

public sealed record Report911CompanySizeRow(
    string CompanySize,
    int Total
);

public sealed record Report911DisabilitySummary(
    int Male,
    int Female,
    int Total
);

public sealed record Report911ShiftRow(
    string Shift,
    int Total
);

public sealed record Report911Response(
    ReportPeriodSummary Period,
    string TramiteType,
    IReadOnlyList<Report911CareerGenderRow> ByCareerAndGender,
    IReadOnlyList<Report911SectorRow> ByCompanySector,
    IReadOnlyList<Report911CompanySizeRow> ByCompanySize,
    Report911DisabilitySummary WithDisability,
    IReadOnlyList<Report911ShiftRow> ByShift,
    int WithInclusionActivities,
    int TotalBeneficiaries
);

public sealed record NonConformanceCategoryCount(
    string Category,
    int Total
);

public sealed record NonConformanceIndicatorResponse(
    ReportPeriodSummary Period,
    string TramiteType,
    int Applicants,
    int Released,
    int NonCompliant,
    int OpenNonConformances,
    decimal ReleaseRate,
    IReadOnlyList<NonConformanceCategoryCount> ByCategory
);

public sealed record CycleTimeTransitionRow(
    string From,
    string To,
    decimal AverageDays,
    int Samples
);

public sealed record CycleTimesResponse(
    string TramiteType,
    IReadOnlyList<CycleTimeTransitionRow> Transitions
);

// --- Analítico Multidimensional ---

public sealed record AnalyticsFilterQuery(
    string TramiteType = "social_service",
    string TimeScope = "period",
    long? PeriodId = null,
    long? ComparePeriodId = null,
    string? SchoolYear = null,
    string? CompareSchoolYear = null,
    string StatusGroup = "in_progress",
    string? Status = null,
    long? CareerId = null,
    int? Semester = null,
    long? CompanyId = null,
    string? CompanyType = null,
    string? Sector = null,
    long? ActivityTypeId = null,
    string? Gender = null
);

public sealed record AnalyticsScopeSummary(
    string TramiteType,
    string TimeScope,
    ReportPeriodSummary? Period,
    string? SchoolYear,
    ReportPeriodSummary? ComparePeriod,
    string? CompareSchoolYear,
    string StatusGroup,
    string? Status,
    IReadOnlyList<string> IncludedPeriodCodes
);

public sealed record GenderBreakdown(
    int Male,
    int Female,
    int Unspecified
);

public sealed record AnalyticsComparisonDelta(
    string PreviousLabel,
    int PreviousUniqueStudents,
    int DeltaStudents,
    decimal? DeltaPercentage,
    int PreviousReleased,
    decimal PreviousReleaseRate
);

public sealed record AnalyticsSummary(
    int UniqueStudents,
    int TotalTramites,
    int SocialServiceStudents,
    int InternshipStudents,
    int InProgressCount,
    int ActiveCount,
    int ReleasedCount,
    int PncCount,
    int CancelledCount,
    decimal ReleaseRatePercentage,
    int DistinctCompanies,
    int UnassignedCompanyCount,
    int UnclassifiedActivityCount,
    GenderBreakdown ByGender,
    AnalyticsComparisonDelta? Comparison
);

public sealed record CareerAnalyticsRow(
    long CareerId,
    string CareerCode,
    string CareerName,
    bool IsActive,
    int Total,
    int Male,
    int Female,
    int Unspecified,
    int InProgress,
    int Released,
    int Pnc,
    int SocialServiceCount,
    int InternshipCount
);

public sealed record SemesterAnalyticsRow(
    int Semester,
    int Total,
    int Male,
    int Female,
    int Unspecified,
    int SocialServiceCount,
    int InternshipCount
);

public sealed record ActivityTypeAnalyticsRow(
    long? ActivityTypeId,
    string Code,
    string Name,
    int Total,
    int Male,
    int Female,
    int Unspecified,
    int SocialServiceCount,
    int InternshipCount,
    decimal SharePercentage
);

public sealed record CompanyAnalyticsRow(
    long? CompanyId,
    string CompanyName,
    string? CompanyType,
    string? Sector,
    bool IsTecnmInternal,
    int Total,
    int ActiveCount,
    int ReleasedCount,
    int Projects,
    int Male,
    int Female,
    int Unspecified,
    string? TopActivityName
);

public sealed record StatusCountRow(
    string Status,
    int Count,
    int SocialServiceCount,
    int InternshipCount
);

public sealed record TrendPointRow(
    string Key,
    string Label,
    string SubLabel,
    int Total,
    int SocialServiceCount,
    int InternshipCount,
    int InProgressCount,
    int ReleasedCount
);

public sealed record AnalyticsReportResponse(
    AnalyticsScopeSummary Scope,
    AnalyticsSummary Summary,
    IReadOnlyList<StatusCountRow> ByStatus,
    IReadOnlyList<CareerAnalyticsRow> ByCareer,
    IReadOnlyList<SemesterAnalyticsRow> BySemester,
    IReadOnlyList<ActivityTypeAnalyticsRow> ByActivityType,
    IReadOnlyList<CompanyAnalyticsRow> TopCompanies,
    IReadOnlyList<TrendPointRow> Trend
);

