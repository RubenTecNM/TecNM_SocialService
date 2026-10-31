namespace TecNM.Api.Core.Entities;

public sealed class StudentImportBatch : AuditableEntity
{
    public long PeriodId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public int TotalRecords { get; set; }
    public int ImportedRecords { get; set; }
    public int FailedRecords { get; set; }
    public string Status { get; set; } = "pending";
    public string? Notes { get; set; }

    public AcademicPeriod? Period { get; set; }
}

