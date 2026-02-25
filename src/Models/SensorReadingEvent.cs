namespace AnalysisService.Models;

// Espelho do SensorReadingEvent publicado pelo IngestionService
public record SensorReadingEvent(
    Guid ReadingId,
    Guid FieldId,
    double SoilHumidity,
    double Temperature,
    double Precipitation,
    DateTime RecordedAt
);
