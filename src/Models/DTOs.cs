namespace AnalysisService.Models;

public record AlertResponse(
    Guid Id,
    Guid FieldId,
    string Type,
    string Severity,
    string Message,
    bool IsActive,
    DateTime TriggeredAt,
    DateTime? ResolvedAt
);

public record FieldStatusResponse(
    Guid FieldId,
    string Status,
    double? LastSoilHumidity,
    double? LastTemperature,
    double? LastPrecipitation,
    DateTime? LastReadingAt,
    DateTime UpdatedAt,
    IEnumerable<AlertResponse> ActiveAlerts
);

public record DashboardResponse(
    IEnumerable<FieldStatusResponse> Fields,
    int TotalActiveAlerts,
    DateTime GeneratedAt
);
