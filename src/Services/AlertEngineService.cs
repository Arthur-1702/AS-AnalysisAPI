using AnalysisService.Data;
using AnalysisService.Models;
using Microsoft.EntityFrameworkCore;

namespace AnalysisService.Services;

public class AlertEngineService(
    AnalysisDbContext db,
    DroughtRuleHandler droughtRule,
    PestRiskRuleHandler pestRule,
    FloodRiskRuleHandler floodRule,
    ILogger<AlertEngineService> logger)
{
    public async Task ProcessAsync(SensorReadingEvent evt, CancellationToken ct = default)
    {
        logger.LogInformation(
            "Processando evento. FieldId={FieldId} Humidity={Humidity} Temp={Temp} Precip={Precip}",
            evt.FieldId, evt.SoilHumidity, evt.Temperature, evt.Precipitation);

        // Executa todas as regras em paralelo
        var ruleResults = await Task.WhenAll(
            droughtRule.EvaluateAsync(evt, ct),
            pestRule.EvaluateAsync(evt, ct),
            floodRule.EvaluateAsync(evt, ct)
        );

        // Persiste novos alertas gerados pelas regras
        var newAlerts = ruleResults.Where(a => a is not null).Cast<Alert>().ToList();
        if (newAlerts.Count > 0)
        {
            db.Alerts.AddRange(newAlerts);
            logger.LogWarning("{Count} alerta(s) gerado(s) para FieldId={FieldId}.",
                newAlerts.Count, evt.FieldId);
        }

        // Atualiza (ou cria) o status do talhão
        await UpdateFieldStatusAsync(evt, newAlerts, ct);

        await db.SaveChangesAsync(ct);
    }

    private async Task UpdateFieldStatusAsync(
        SensorReadingEvent evt, List<Alert> newAlerts, CancellationToken ct)
    {
        var status = await db.FieldStatuses.FindAsync([evt.FieldId], ct)
            ?? new FieldStatus { FieldId = evt.FieldId };

        status.LastSoilHumidity = evt.SoilHumidity;
        status.LastTemperature = evt.Temperature;
        status.LastPrecipitation = evt.Precipitation;
        status.LastReadingAt = evt.RecordedAt;
        status.UpdatedAt = DateTime.UtcNow;

        // Prioridade: Seca > Praga > Alagamento > Normal
        status.Status = newAlerts.Any(a => a.Type == AlertType.DroughtRisk) ? FieldStatusType.DroughtAlert
            : newAlerts.Any(a => a.Type == AlertType.PestRisk)              ? FieldStatusType.PestRisk
            : newAlerts.Any(a => a.Type == AlertType.FloodRisk)             ? FieldStatusType.FloodRisk
            : await HasActiveAlertsAsync(evt.FieldId, ct)
                ? await GetCurrentStatusAsync(evt.FieldId, ct)
                : FieldStatusType.Normal;

        db.FieldStatuses.Update(status);
    }

    private async Task<bool> HasActiveAlertsAsync(Guid fieldId, CancellationToken ct) =>
        await db.Alerts.AnyAsync(a => a.FieldId == fieldId && a.IsActive, ct);

    private async Task<FieldStatusType> GetCurrentStatusAsync(Guid fieldId, CancellationToken ct)
    {
        var latestAlert = await db.Alerts
            .Where(a => a.FieldId == fieldId && a.IsActive)
            .OrderByDescending(a => a.TriggeredAt)
            .FirstOrDefaultAsync(ct);

        return latestAlert?.Type switch
        {
            AlertType.DroughtRisk => FieldStatusType.DroughtAlert,
            AlertType.PestRisk    => FieldStatusType.PestRisk,
            AlertType.FloodRisk   => FieldStatusType.FloodRisk,
            _                     => FieldStatusType.Normal
        };
    }
}
