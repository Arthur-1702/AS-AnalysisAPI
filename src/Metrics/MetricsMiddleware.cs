using Prometheus;

namespace AnalysisService.Metrics;

public class MetricsMiddleware(RequestDelegate next)
{
    private static readonly Counter AlertsGenerated = Prometheus.Metrics
        .CreateCounter("analysis_alerts_generated_total", "Total de alertas gerados",
            new CounterConfiguration { LabelNames = ["type"] });

    private static readonly Counter AlertsResolved = Prometheus.Metrics
        .CreateCounter("analysis_alerts_resolved_total", "Total de alertas resolvidos");

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
}
