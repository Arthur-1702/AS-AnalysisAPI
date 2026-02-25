using AnalysisService.Data;
using AnalysisService.Models;
using Microsoft.EntityFrameworkCore;

namespace AnalysisService.Services;

/// <summary>
/// Regra: precipitação acima de 50mm em uma única leitura
/// indica risco de alagamento.
/// </summary>
public class FloodRiskRuleHandler(AnalysisDbContext db, ILogger<FloodRiskRuleHandler> logger)
{
    private const double PrecipitationThreshold = 50.0;

    public async Task<Alert?> EvaluateAsync(SensorReadingEvent evt, CancellationToken ct = default)
    {
        if (evt.Precipitation < PrecipitationThreshold)
            return null;

        var existingAlert = await db.Alerts
            .AnyAsync(a => a.FieldId == evt.FieldId
                        && a.Type == AlertType.FloodRisk
                        && a.IsActive, ct);

        if (existingAlert)
            return null;

        logger.LogWarning(
            "RISCO DE ALAGAMENTO: FieldId={FieldId} — Precipitação={Precip}mm.",
            evt.FieldId, evt.Precipitation);

        return new Alert
        {
            FieldId  = evt.FieldId,
            Type     = AlertType.FloodRisk,
            Severity = AlertSeverity.Critical,
            Message  = $"Precipitação de {evt.Precipitation:F1}mm — risco de alagamento do talhão.",
            TriggeredAt = evt.RecordedAt
        };
    }
}
