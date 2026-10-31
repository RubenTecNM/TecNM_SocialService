namespace TecNM.Api.Modules.Students;

public sealed record StudentListItem(
    long Id,
    string ControlNumber,
    string FullName,
    long CareerId,
    string CareerCode,
    int CurrentSemester,
    decimal CurriculumProgressPercentage,
    string Shift,
    bool IsEnrolled,
    bool HasValidImss,
    bool HasUserAccount);

public sealed record StudentDetail(
    long Id,
    long? UserId,
    long CareerId,
    string CareerName,
    long? ImportBatchId,
    string ControlNumber,
    string? Curp,
    string FirstName,
    string FirstSurname,
    string? SecondSurname,
    string? Gender,
    string Shift,
    bool HasDisability,
    string? DisabilityDescription,
    string? InstitutionalEmail,
    string? PersonalEmail,
    string? PhoneNumber,
    int CurrentSemester,
    int CompletedCredits,
    decimal CurriculumProgressPercentage,
    bool IsEnrolled,
    bool HasValidImss,
    bool HasImssFile,
    DateTimeOffset? ImssValidatedAt,
    string? ImssValidatedByName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record StudentRequest(
    long CareerId,
    string ControlNumber,
    string? Curp,
    string FirstName,
    string FirstSurname,
    string? SecondSurname,
    string? Gender,
    string Shift = "morning",
    bool HasDisability = false,
    string? DisabilityDescription = null,
    string? InstitutionalEmail = null,
    string? PersonalEmail = null,
    string? PhoneNumber = null,
    int CurrentSemester = 7,
    int CompletedCredits = 156,
    bool IsEnrolled = true);

public sealed record StudentContactRequest(
    string? PersonalEmail,
    string? InstitutionalEmail,
    string? PhoneNumber,
    int? CurrentSemester = null,
    string? Shift = null);

public sealed record ValidateImssRequest(bool IsValid);

public sealed record CreateStudentAccountResponse(
    long UserId,
    string Username,
    string TemporaryPassword);

public sealed record EligibilityReason(string Code, string Message);

public sealed record EligibilityResult(
    bool Eligible,
    IReadOnlyList<EligibilityReason> Reasons);

public sealed record StudentEligibility(
    EligibilityResult SocialService,
    EligibilityResult Internship);

public sealed record StudentImportBatchDto(
    long Id,
    long PeriodId,
    string PeriodCode,
    string FileName,
    int TotalRecords,
    int ImportedRecords,
    int FailedRecords,
    string Status,
    string? Notes,
    DateTimeOffset CreatedAt,
    string? CreatedByName);

public sealed record StudentImportRowError(
    int Row,
    string? ControlNumber,
    string? Field,
    string Message);

public sealed record StudentImportResult(
    long Id,
    long PeriodId,
    string PeriodCode,
    string FileName,
    int TotalRecords,
    int ImportedRecords,
    int FailedRecords,
    string Status,
    string? Notes,
    DateTimeOffset CreatedAt,
    string? CreatedByName,
    IReadOnlyList<StudentImportRowError> RowErrors);

