namespace TecNM.Api.Core.Entities;

public sealed class ActivityType : AuditableEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string AppliesTo { get; set; } = "both";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
