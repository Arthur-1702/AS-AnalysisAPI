namespace AnalysisService.Models;

public enum AlertType
{
    DroughtRisk,    // umidade < 30% por mais de 24h
    PestRisk,       // temperatura alta + baixa precipitação
    FloodRisk       // precipitação acima de 50mm
}

public enum AlertSeverity
{
    Warning,
    Critical
}

public class Alert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FieldId { get; set; }
    public AlertType Type { get; set; }
    public AlertSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}
