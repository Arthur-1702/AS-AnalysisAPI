using AnalysisService.Data;
using AnalysisService.Models;
using Microsoft.EntityFrameworkCore;

namespace AnalysisService.Services;

public class DashboardService(AnalysisDbContext db)
{
    public async Task<DashboardResponse> GetDashboardAsync(
        IEnumerable<Guid> fieldIds, CancellationToken ct = default)
    {
        var ids = fieldIds.ToList();

        var statuses = await db.FieldStatuses
            .AsNoTracking()
            .Where(fs => ids.Contains(fs.FieldId))
            .ToListAsync(ct);

        var activeAlerts = await db.Alerts
            .AsNoTracking()
            .Where(a => ids.Contains(a.FieldId) && a.IsActive)
            .OrderByDescending(a => a.TriggeredAt)
            .ToListAsync(ct);

        var alertsByField = activeAlerts
            .GroupBy(a => a.FieldId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var fieldResponses = ids.Select(fieldId =>
        {
            var status = statuses.FirstOrDefault(s => s.FieldId == fieldId)
                ?? new FieldStatus { FieldId = fieldId };

            var alerts = alertsByField.GetValueOrDefault(fieldId, []);

            return new FieldStatusResponse(
                FieldId: fieldId,
                Status: status.Status.ToString(),
                LastSoilHumidity: status.LastSoilHumidity,
                LastTemperature: status.LastTemperature,
                LastPrecipitation: status.LastPrecipitation,
                LastReadingAt: status.LastReadingAt,
                UpdatedAt: status.UpdatedAt,
                ActiveAlerts: alerts.Select(MapAlertToResponse)
            );
        });

        return new DashboardResponse(
            Fields: fieldResponses,
            TotalActiveAlerts: activeAlerts.Count,
            GeneratedAt: DateTime.UtcNow
        );
    }

    public async Task<IEnumerable<AlertResponse>> GetAlertsAsync(
        Guid fieldId, bool onlyActive, int limit, CancellationToken ct = default)
    {
        var query = db.Alerts
            .AsNoTracking()
            .Where(a => a.FieldId == fieldId);

        if (onlyActive)
            query = query.Where(a => a.IsActive);

        var alerts = await query
            .OrderByDescending(a => a.TriggeredAt)
            .Take(Math.Min(limit, 500))
            .ToListAsync(ct);

        return alerts.Select(MapAlertToResponse);
    }

    public async Task<AlertResponse?> ResolveAlertAsync(Guid alertId, CancellationToken ct = default)
    {
        var alert = await db.Alerts.FindAsync([alertId], ct);
        if (alert is null || !alert.IsActive) return null;

        alert.IsActive = false;
        alert.ResolvedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return MapAlertToResponse(alert);
    }

    private static AlertResponse MapAlertToResponse(Alert a) => new(
        a.Id,
        a.FieldId,
        a.Type.ToString(),
        a.Severity.ToString(),
        a.Message,
        a.IsActive,
        a.TriggeredAt,
        a.ResolvedAt
    );
}
