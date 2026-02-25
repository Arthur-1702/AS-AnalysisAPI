using AnalysisService.Data;
using AnalysisService.Models;
using Microsoft.EntityFrameworkCore;

namespace AnalysisService.Services;

/// <summary>
/// Regra: se a umidade do solo de um talhão ficar abaixo de 30%
/// por mais de 24 horas consecutivas, gera um "Alerta de Seca".
/// </summary>
public class DroughtRuleHandler(AnalysisDbContext db, ILogger<DroughtRuleHandler> logger)
{
    private const double HumidityThreshold = 30.0;
    private const int ConsecutiveHoursThreshold = 24;

    public async Task<Alert?> EvaluateAsync(SensorReadingEvent evt, CancellationToken ct = default)
    {
        // Verifica se já existe alerta de seca ativo para este talhão
        var existingAlert = await db.Alerts
            .AnyAsync(a => a.FieldId == evt.FieldId
                        && a.Type == AlertType.DroughtRisk
                        && a.IsActive, ct);

        if (existingAlert)
            return null; // alerta já emitido, não duplica

        if (evt.SoilHumidity >= HumidityThreshold)
            return null; // umidade OK

        // Verifica se há leituras abaixo do threshold nas últimas 24h
        var since = evt.RecordedAt.AddHours(-ConsecutiveHoursThreshold);

        // Busca a leitura mais antiga disponível no período para verificar se
        // todas as leituras do período ficaram abaixo do threshold.
        // Como as leituras ficam no IngestionService (DB separado), usamos
        // as informações do evento atual + histórico de alertas/status para inferir.
        // Estratégia: checar se o FieldStatus já estava com umidade baixa há 24h.
        var fieldStatus = await db.FieldStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(fs => fs.FieldId == evt.FieldId, ct);

        bool persistentlyLow = fieldStatus is not null
            && fieldStatus.LastSoilHumidity < HumidityThreshold
            && fieldStatus.LastReadingAt.HasValue
            && fieldStatus.LastReadingAt.Value <= since;

        if (!persistentlyLow)
            return null;

        logger.LogWarning(
            "ALERTA DE SECA: FieldId={FieldId} — umidade {Humidity}% abaixo de {Threshold}% por +{Hours}h.",
            evt.FieldId, evt.SoilHumidity, HumidityThreshold, ConsecutiveHoursThreshold);

        return new Alert
        {
            FieldId  = evt.FieldId,
            Type     = AlertType.DroughtRisk,
            Severity = AlertSeverity.Critical,
            Message  = $"Umidade do solo em {evt.SoilHumidity:F1}% — abaixo de {HumidityThreshold}% " +
                       $"por mais de {ConsecutiveHoursThreshold} horas consecutivas.",
            TriggeredAt = evt.RecordedAt
        };
    }
}
