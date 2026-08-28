namespace Clinic.Api.Domain;

/// <summary>Audit record of one Excel import.</summary>
public class ExcelSyncLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = null!;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public int TotalRows { get; set; }
    public int Imported { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }

    /// <summary>JSON array of [{row, error}] describing rejected rows.</summary>
    public string RowErrorsJson { get; set; } = "[]";
}
