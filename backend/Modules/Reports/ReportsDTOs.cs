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
    bool Ct
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
    string? TargetCommunity
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

