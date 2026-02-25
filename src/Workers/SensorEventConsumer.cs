using System.Text.Json;
using AnalysisService.Models;
using AnalysisService.Services;
using Azure.Messaging.ServiceBus;
using Prometheus;
using PrometheusMetrics = Prometheus.Metrics;

namespace AnalysisService.Workers;

/// <summary>
/// Background worker que consome eventos do tópico sensor-readings
/// (subscription: analysis-sub) e aciona o AlertEngineService.
/// </summary>
public class SensorEventConsumer : IHostedService, IAsyncDisposable
{
    private readonly ServiceBusProcessor _processor;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SensorEventConsumer> _logger;

    private static readonly Counter EventsConsumed = PrometheusMetrics
        .CreateCounter("analysis_events_consumed_total", "Eventos consumidos do Service Bus",
            new CounterConfiguration { LabelNames = ["result"] });

    private static readonly Histogram ProcessingDuration = PrometheusMetrics
        .CreateHistogram("analysis_event_processing_duration_seconds",
            "Duração do processamento de cada evento");

    public SensorEventConsumer(
        IConfiguration config,
        IServiceScopeFactory scopeFactory,
        ILogger<SensorEventConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var connectionString = config["ServiceBus:ConnectionString"]
            ?? throw new InvalidOperationException("ServiceBus:ConnectionString não configurado.");
        var topicName = config["ServiceBus:TopicName"] ?? "sensor-readings";
        var subscriptionName = config["ServiceBus:SubscriptionName"] ?? "analysis-sub";

        var client = new ServiceBusClient(connectionString);
        _processor = client.CreateProcessor(topicName, subscriptionName, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 4,
            PrefetchCount = 10
        });

        _processor.ProcessMessageAsync += HandleMessageAsync;
        _processor.ProcessErrorAsync += HandleErrorAsync;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _logger.LogInformation("SensorEventConsumer iniciando...");
        await _processor.StartProcessingAsync(ct);
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _logger.LogInformation("SensorEventConsumer parando...");
        await _processor.StopProcessingAsync(ct);
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        using var timer = ProcessingDuration.NewTimer();

        try
        {
            var body = args.Message.Body.ToString();
            var evt = JsonSerializer.Deserialize<SensorReadingEvent>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (evt is null)
            {
                _logger.LogWarning("Mensagem com corpo inválido. MessageId={MessageId}", args.Message.MessageId);
                await args.DeadLetterMessageAsync(args.Message, "InvalidPayload", "Não foi possível deserializar o evento.");
                EventsConsumed.WithLabels("dead_letter").Inc();
                return;
            }

            // Cria um scope para injetar serviços Scoped dentro de um Singleton/HostedService
            using var scope = _scopeFactory.CreateScope();
            var engine = scope.ServiceProvider.GetRequiredService<AlertEngineService>();

            await engine.ProcessAsync(evt, args.CancellationToken);
            await args.CompleteMessageAsync(args.Message);

            EventsConsumed.WithLabels("success").Inc();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar mensagem. MessageId={MessageId}", args.Message.MessageId);
            EventsConsumed.WithLabels("error").Inc();

            // Abandona para retentar (Service Bus fará retry automaticamente)
            await args.AbandonMessageAsync(args.Message);
        }
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception,
            "Erro no ServiceBusProcessor. Source={Source} EntityPath={EntityPath}",
            args.ErrorSource, args.EntityPath);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _processor.DisposeAsync();
}
