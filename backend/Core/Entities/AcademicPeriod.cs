namespace TecNM.Api.Core.Entities;

public sealed class AcademicPeriod : AuditableEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string SchoolYear { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsActive { get; set; }
}
