namespace TecNM.Api.Core.Entities;

public class TramiteStatusLog : AuditableEntity
{
    public string TramiteType { get; set; } = string.Empty;
    public long TramiteId { get; set; }
    public string? PreviousStatus { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public long ChangedBy { get; set; }

    public User? ChangedByUser { get; set; }
}

public class NonConformance : AuditableEntity
{
    public string TramiteType { get; set; } = string.Empty;
    public long TramiteId { get; set; }
    public long StudentId { get; set; }
    public long PeriodId { get; set; }
    public string IncidentCode { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "open";
    public string? ActionPlan { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public long? ResolvedBy { get; set; }

    public Student? Student { get; set; }
    public AcademicPeriod? Period { get; set; }
    public User? Resolver { get; set; }
}

