namespace TecNM.Api.Modules.Catalogs;

// --- Carreras ---
public sealed record CareerResponse(
    long Id,
    string Code,
    string Name,
    int TotalCredits,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CareerRequest(
    string Code,
    string Name,
    int TotalCredits = 260);

public sealed record UpdateStatusRequest(bool IsActive);

// --- Periodos Académicos ---
public sealed record AcademicPeriodResponse(
    long Id,
    string Code,
    string Name,
    string SchoolYear,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AcademicPeriodRequest(
    string Code,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    string? SchoolYear = null);

public sealed record SchoolYearPeriodItem(
    long Id,
    string Code,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsActive);

public sealed record SchoolYearOptionResponse(
    string SchoolYear,
    bool HasActivePeriod,
    IReadOnlyList<SchoolYearPeriodItem> Periods);

// --- Tipos de Actividad ---
public sealed record ActivityTypeResponse(
    long Id,
    string Code,
    string Name,
    string AppliesTo,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ActivityTypeRequest(
    string Code,
    string Name,
    string AppliesTo = "both",
    string? Description = null);

// --- Programas de Servicio Social ---
public sealed record SocialServiceProgramResponse(
    long Id,
    string Code,
    string Name,
    string Category,
    bool IsInternal,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record SocialServiceProgramRequest(
    string Code,
    string Name,
    string Category,
    bool IsInternal = true,
    string? Description = null);

// --- Autoridades Institucionales ---
public sealed record InstitutionalAuthorityResponse(
    long Id,
    string RoleKey,
    string? Title,
    string FullName,
    string Position,
    string? Email,
    string? PhoneNumber,
    string? SignatureInitials,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record InstitutionalAuthorityRequest(
    string RoleKey,
    string? Title,
    string FullName,
    string Position,
    string? Email,
    string? PhoneNumber,
    string? SignatureInitials);

// --- Series de Folio ---
public sealed record DocumentSeriesResponse(
    long Id,
    string DocumentType,
    string Prefix,
    int CurrentYear,
    int CurrentSequence,
    string FormatPattern,
    string? Description,
    string NextFolioPreview,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record DocumentSeriesRequest(
    string DocumentType,
    string Prefix,
    int CurrentYear,
    int CurrentSequence,
    string FormatPattern,
    string? Description);
