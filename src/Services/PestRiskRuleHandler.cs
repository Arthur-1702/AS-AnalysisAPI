using AnalysisService.Data;
using AnalysisService.Models;
using Microsoft.EntityFrameworkCore;

namespace AnalysisService.Services;

/// <summary>
/// Regra: temperatura acima de 35°C combinada com
/// precipitação abaixo de 5mm indica risco de pragas.
/// </summary>
public class PestRiskRuleHandler(AnalysisDbContext db, ILogger<PestRiskRuleHandler> logger)
{
    private const double TemperatureThreshold = 35.0;
    private const double PrecipitationThreshold = 5.0;

    public async Task<Alert?> EvaluateAsync(SensorReadingEvent evt, CancellationToken ct = default)
    {
        if (evt.Temperature < TemperatureThreshold || evt.Precipitation >= PrecipitationThreshold)
            return null;

        var existingAlert = await db.Alerts
            .AnyAsync(a => a.FieldId == evt.FieldId
                        && a.Type == AlertType.PestRisk
                        && a.IsActive, ct);

        if (existingAlert)
            return null;

        logger.LogWarning(
            "RISCO DE PRAGA: FieldId={FieldId} — Temp={Temp}°C, Precipitação={Precip}mm.",
            evt.FieldId, evt.Temperature, evt.Precipitation);

        return new Alert
        {
            FieldId  = evt.FieldId,
            Type     = AlertType.PestRisk,
            Severity = AlertSeverity.Warning,
            Message  = $"Temperatura de {evt.Temperature:F1}°C e precipitação de {evt.Precipitation:F1}mm " +
                       $"indicam condições favoráveis ao desenvolvimento de pragas.",
            TriggeredAt = evt.RecordedAt
        };
    }
}
