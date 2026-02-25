# AnalysisService — AgroMonitor

Microsserviço responsável por consumir eventos de sensores do **Azure Service Bus**,
processar regras de alerta e disponibilizar o dashboard de monitoramento via API REST.

---

## Arquitetura Interna

```
Azure Service Bus
  Topic: sensor-readings
  Subscription: analysis-sub
         │
         ▼
SensorEventConsumer (IHostedService)
         │
         ▼
AlertEngineService
    ├── DroughtRuleHandler   → umidade < 30% por +24h  → AlertType.DroughtRisk   (Critical)
    ├── PestRiskRuleHandler  → temp > 35°C + precip < 5mm → AlertType.PestRisk   (Warning)
    └── FloodRiskRuleHandler → precipitação > 50mm     → AlertType.FloodRisk     (Critical)
         │
         ▼
AnalysisDbContext (Azure SQL: agromonitor-analysis)
  ├── Alerts
  └── FieldStatuses

REST API (JWT)
  ├── GET  /dashboard?fieldIds=...   → DashboardController
  ├── GET  /alerts/{fieldId}         → AlertsController
  └── PATCH /alerts/{alertId}/resolve
```

---

## Endpoints

| Método | Rota                        | Descrição                                  | Auth |
| ------ | --------------------------- | ------------------------------------------ | ---- |
| GET    | `/dashboard`                | Dashboard consolidado por lista de talhões | JWT  |
| GET    | `/alerts/{fieldId}`         | Alertas de um talhão                       | JWT  |
| PATCH  | `/alerts/{alertId}/resolve` | Marca alerta como resolvido                | JWT  |
| GET    | `/health`                   | Health check                               | —    |
| GET    | `/metrics`                  | Métricas Prometheus                        | —    |
| GET    | `/swagger`                  | Documentação interativa                    | —    |

---

## Exemplos de Uso

### GET `/dashboard?fieldIds=id1&fieldIds=id2`

**Response:**

```json
{
  "fields": [
    {
      "fieldId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "status": "DroughtAlert",
      "lastSoilHumidity": 22.5,
      "lastTemperature": 34.1,
      "lastPrecipitation": 0.0,
      "lastReadingAt": "2024-03-15T10:30:00Z",
      "updatedAt": "2024-03-15T10:30:05Z",
      "activeAlerts": [
        {
          "id": "...",
          "fieldId": "...",
          "type": "DroughtRisk",
          "severity": "Critical",
          "message": "Umidade do solo em 22.5% — abaixo de 30% por mais de 24 horas consecutivas.",
          "isActive": true,
          "triggeredAt": "2024-03-15T10:30:05Z",
          "resolvedAt": null
        }
      ]
    }
  ],
  "totalActiveAlerts": 1,
  "generatedAt": "2024-03-15T10:30:10Z"
}
```

---

## Regras de Alerta

| Regra                   | Condição                                       | Severidade |
| ----------------------- | ---------------------------------------------- | ---------- |
| **Alerta de Seca**      | Umidade < 30% persistindo por mais de 24 horas | Critical   |
| **Risco de Praga**      | Temperatura > 35°C **e** Precipitação < 5mm    | Warning    |
| **Risco de Alagamento** | Precipitação > 50mm em uma leitura             | Critical   |

Alertas do mesmo tipo não são duplicados enquanto o alerta anterior ainda estiver ativo.
Use `PATCH /alerts/{alertId}/resolve` para fechar um alerta e permitir que a regra seja disparada novamente.

---

## Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- **Azure Service Bus** com topic `sensor-readings` e subscription `analysis-sub`
  (criados durante o setup do IngestionService)
- `dotnet-ef` instalado: `dotnet tool install --global dotnet-ef`

---

## Passo a Passo — Ambiente Local

### 1. Verificar que o topic e subscription existem

```bash
# Listar subscriptions do topic
az servicebus topic subscription list \
  --resource-group rg-agromonitor \
  --namespace-name agromonitor-bus \
  --topic-name sensor-readings \
  --query "[].name" -o tsv
```

Se `analysis-sub` não aparecer:

```bash
az servicebus topic subscription create \
  --resource-group rg-agromonitor \
  --namespace-name agromonitor-bus \
  --topic-name sensor-readings \
  --name analysis-sub
```

---

### 2. Criar o banco de dados

Conecte em `localhost,1433` com `fiapas` / `123@login` e execute:

```sql
CREATE DATABASE [agromonitor-analysis];
```

---

### 3. Preencher a connection string do Service Bus

Edite `appsettings.Development.json` com a connection string obtida no setup do IngestionService.

---

### 4. Gerar e aplicar a Migration

```bash
dotnet ef migrations add InitialCreate \
  --project AnalysisService.csproj \
  --output-dir src/Data/Migrations

dotnet ef database update
```

---

### 5. Rodar o serviço

```bash
dotnet run
```

Ao iniciar, o `SensorEventConsumer` começa a escutar o topic automaticamente em background.

Acesse:

- Swagger UI: http://localhost:5004/swagger
- Metrics: http://localhost:5004/metrics

---

## Docker Compose

```bash
# Preencha ServiceBus__ConnectionString antes de subir
docker compose up --build
```

---

## Passo a Passo — Azure (Produção)

### 1. Criar banco no Azure SQL Server existente

```bash
az sql db create \
  --resource-group rg-agromonitor \
  --server agromonitor-sqlsrv \
  --name agromonitor-analysis \
  --service-objective Basic
```

### 2. Atualizar connection string

```json
"AnalysisDb": "Server=agromonitor-sqlsrv.database.windows.net;Database=agromonitor-analysis;User Id=fiapas;Password=123@login;Encrypt=True;"
```

### 3. Aplicar migration

```bash
dotnet ef database update
```

---

## Variáveis de Ambiente

| Variável                        | Descrição                                     |
| ------------------------------- | --------------------------------------------- |
| `ConnectionStrings__AnalysisDb` | Connection string do Azure SQL                |
| `ServiceBus__ConnectionString`  | Connection string do Azure Service Bus        |
| `ServiceBus__TopicName`         | Nome do topic (padrão: `sensor-readings`)     |
| `ServiceBus__SubscriptionName`  | Nome da subscription (padrão: `analysis-sub`) |
| `Jwt__Secret`                   | Mesma chave usada no IdentityService          |
| `Jwt__Issuer`                   | Mesmo issuer (ex: `IdentityService`)          |
| `Jwt__Audience`                 | Mesmo audience (ex: `AgroMonitor`)            |

---

## Observabilidade

| Métrica                                      | Tipo      | Descrição                           |
| -------------------------------------------- | --------- | ----------------------------------- |
| `analysis_events_consumed_total`             | Counter   | Eventos consumidos do Service Bus   |
| `analysis_event_processing_duration_seconds` | Histogram | Duração do processamento por evento |
| `analysis_alerts_generated_total`            | Counter   | Total de alertas gerados (por tipo) |
| `analysis_alerts_resolved_total`             | Counter   | Total de alertas resolvidos         |
| `analysis_http_request_duration_seconds`     | Histogram | Latência das requisições HTTP       |

Configure o `prometheus.yml` para fazer scrape em `http://analysis-service:8080/metrics`.
