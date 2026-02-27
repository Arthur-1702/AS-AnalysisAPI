using Prometheus;

namespace AnalysisService.Metrics;

public class MetricsMiddleware(RequestDelegate next)
{
    // ─── Business Metrics (Alerts & Status) ──────────────────────────────
    private static readonly Counter AlertsGenerated = Prometheus.Metrics
        .CreateCounter("analysis_alerts_generated_total", "Total de alertas gerados",
            new CounterConfiguration { LabelNames = ["field_id", "type"] });

    private static readonly Counter AlertsResolved = Prometheus.Metrics
        .CreateCounter("analysis_alerts_resolved_total", "Total de alertas resolvidos");

    private static readonly Gauge ActiveAlerts = Prometheus.Metrics
        .CreateGauge("analysis_active_alerts", "Quantidade de alertas ativos",
            new GaugeConfiguration { LabelNames = ["field_id", "type"] });

    private static readonly Gauge FieldStatus = Prometheus.Metrics
        .CreateGauge("analysis_field_status", "Status do talhão (0=Normal, 1=Seca, 2=Praga, 3=Alagamento)",
            new GaugeConfiguration { LabelNames = ["field_id"] });

    // ─── Sensor Metrics ──────────────────────────────────────────────────
    private static readonly Gauge SoilHumidity = Prometheus.Metrics
        .CreateGauge("analysis_soil_humidity_percent", "Umidade do solo em %",
            new GaugeConfiguration { LabelNames = ["field_id"] });

    private static readonly Gauge Temperature = Prometheus.Metrics
        .CreateGauge("analysis_temperature_celsius", "Temperatura em °C",
            new GaugeConfiguration { LabelNames = ["field_id"] });

    private static readonly Gauge Precipitation = Prometheus.Metrics
        .CreateGauge("analysis_precipitation_mm", "Precipitação em mm",
            new GaugeConfiguration { LabelNames = ["field_id"] });

    // ─── HTTP Metrics ────────────────────────────────────────────────────
    private static readonly Histogram RequestDuration = Prometheus.Metrics
        .CreateHistogram("analysis_http_request_duration_seconds", "Duração das requisições HTTP",
            new HistogramConfiguration { LabelNames = ["method", "path", "status"] });

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        await next(context);

        stopwatch.Stop();

        var path = context.Request.Path.Value ?? "";
        var method = context.Request.Method;
        var statusCode = context.Response.StatusCode.ToString();

        RequestDuration
            .WithLabels(method, SanitizePath(path), statusCode)
            .Observe(stopwatch.Elapsed.TotalSeconds);

        if (method == "PATCH" && path.Contains("/resolve", StringComparison.OrdinalIgnoreCase)
            && context.Response.StatusCode == 200)
        {
            AlertsResolved.Inc();
        }
    }

    private static string SanitizePath(string path) =>
        System.Text.RegularExpressions.Regex.Replace(
            path,
            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
            "{id}"
        );

    // ─── Static Methods for Business Metrics ────────────────────────────
    public static void RecordSensorReading(
        Guid fieldId, double humidity, double temperature, double precipitation)
    {
        SoilHumidity.WithLabels(fieldId.ToString()).Set(humidity);
        Temperature.WithLabels(fieldId.ToString()).Set(temperature);
        Precipitation.WithLabels(fieldId.ToString()).Set(precipitation);
    }

    public static void RecordAlertGenerated(Guid fieldId, string alertType)
    {
        AlertsGenerated.WithLabels(fieldId.ToString(), alertType).Inc();
    }

    public static void RecordFieldStatus(Guid fieldId, int statusCode)
    {
        FieldStatus.WithLabels(fieldId.ToString()).Set(statusCode);
    }

    public static void UpdateActiveAlerts(Guid fieldId, string alertType, int count)
    {
        ActiveAlerts.WithLabels(fieldId.ToString(), alertType).Set(count);
    }
}
