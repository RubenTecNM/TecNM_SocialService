namespace TecNM.Api.Modules.Quality;

public sealed record StatusLogFullResponse(
    long Id,
    string TramiteType,
    long TramiteId,
    string? PreviousStatus,
    string NewStatus,
    string? Reason,
    long ChangedById,
    string ChangedByName,
    DateTimeOffset CreatedAt
);

public sealed record NonConformanceResponse(
    long Id,
    string IncidentCode,
    string TramiteType,
    long TramiteId,
    long StudentId,
    string StudentControlNumber,
    string StudentFullName,
    long PeriodId,
    string PeriodCode,
    string Category,
    string Description,
    string Status,
    string? ActionPlan,
    DateTimeOffset? ResolvedAt,
    string? ResolvedByName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public sealed record CreateNonConformanceRequest(
    string TramiteType,
    long TramiteId,
    string Category,
    string Description,
    string? ActionPlan
);

public sealed record UpdateNonConformanceRequest(
    string Category,
    string Description,
    string? ActionPlan
);

public sealed record UpdateNonConformanceStatusRequest(
    string Status,
    string? ActionPlan
);

