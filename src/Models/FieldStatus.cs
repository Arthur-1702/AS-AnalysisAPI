namespace AnalysisService.Models;

public enum FieldStatusType
{
    Normal,
    DroughtAlert,
    PestRisk,
    FloodRisk
}

public class FieldStatus
{
    public Guid FieldId { get; set; }
    public FieldStatusType Status { get; set; } = FieldStatusType.Normal;
    public double? LastSoilHumidity { get; set; }
    public double? LastTemperature { get; set; }
    public double? LastPrecipitation { get; set; }
    public DateTime? LastReadingAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
