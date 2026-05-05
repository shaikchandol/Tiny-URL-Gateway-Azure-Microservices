# Low-Level Design (LLD) — TinyURL Azure Microservices

## 1. Solution Structure

```
TinyUrl.Microservices.sln
├── src/
│   ├── TinyUrl.AppHost/                          # .NET Aspire orchestration host
│   │   └── Program.cs                            # Defines all services + Azure resources
│   │
│   ├── TinyUrl.ServiceDefaults/                  # Shared cross-cutting defaults
│   │   └── Extensions.cs                         # OpenTelemetry, health checks, service discovery
│   │
│   ├── shared/
│   │   └── TinyUrl.Contracts/                    # Shared event contracts (NuGet-publishable)
│   │       └── Events/
│   │           ├── UrlCreatedEvent.cs
│   │           ├── UrlDeletedEvent.cs
│   │           └── UrlClickedEvent.cs
│   │
│   └── services/
│       ├── TinyUrl.UrlService/                   # URL CRUD Microservice
│       │   ├── TinyUrl.UrlService.Domain/        # Entities (ShortUrl)
│       │   ├── TinyUrl.UrlService.Application/   # CQRS Commands, Queries, Behaviors, DTOs
│       │   ├── TinyUrl.UrlService.Infrastructure/# EF Core, Redis, Service Bus, DI
│       │   └── TinyUrl.UrlService.Api/           # ASP.NET Core, Controllers, Middleware, Program.cs
│       │
│       ├── TinyUrl.RedirectService/              # Redirect Microservice
│       │   └── TinyUrl.RedirectService.Api/      # Redis-first lookup, Service Bus publish
│       │
│       └── TinyUrl.AnalyticsService/             # Analytics Microservice
│           ├── TinyUrl.AnalyticsService.Infrastructure/  # DbContext, Service Bus Consumer
│           └── TinyUrl.AnalyticsService.Api/     # Analytics API endpoints
│
├── deploy/
│   ├── k8s/                                      # Kubernetes manifests
│   │   ├── url-service/      (deployment, service, hpa)
│   │   ├── redirect-service/ (deployment, service, hpa)
│   │   ├── analytics-service/(deployment, service)
│   │   ├── gateway/          (ingress with AGIC + WAF)
│   │   └── shared/           (namespaces, network policies)
│   │
│   └── bicep/                                    # Azure Infrastructure as Code
│       ├── main.bicep                            # Entry point (subscription-scoped)
│       └── modules/
│           ├── aks.bicep                         # AKS with Workload Identity, Defender
│           ├── acr.bicep                         # ACR Premium with content trust
│           ├── keyvault.bicep                    # Key Vault with RBAC, private endpoint
│           ├── postgres.bicep                    # PostgreSQL Flexible, ZR, Entra Auth
│           ├── redis.bicep                       # Redis Premium, ZR, TLS-only
│           ├── servicebus.bicep                  # Service Bus Premium, ZR, local auth disabled
│           └── appinsights.bicep                 # App Insights + Log Analytics Workspace
│
├── pipelines/                                    # Azure DevOps CI/CD
│   ├── url-service-pipeline.yml
│   ├── redirect-service-pipeline.yml
│   ├── analytics-service-pipeline.yml
│   └── infrastructure-pipeline.yml
│
└── design/
    ├── HLD.md
    ├── LLD.md
    └── DesignPatterns.md
```

---

## 2. Domain Model — URL Service

```csharp
public class ShortUrl
{
    // Identity
    public Guid Id { get; private set; }
    public string ShortCode { get; private set; }      // Unique, 7-char Base62
    public string LongUrl { get; private set; }
    public string? CustomAlias { get; private set; }

    // State
    public int ClickCount { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public bool IsDeleted { get; private set; }        // Soft delete
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // Factory — enforces valid initial state
    public static ShortUrl Create(string shortCode, string longUrl, ...)

    // Behaviors
    public void UpdateLongUrl(string url)
    public void UpdateExpiry(DateTimeOffset? expiry)
    public void SoftDelete()
    public bool IsExpired()
}
```

---

## 3. Database Schema

### URL Service DB (`urlservice-db`)

```sql
CREATE TABLE urls (
    id           UUID PRIMARY KEY,
    short_code   VARCHAR(50) UNIQUE NOT NULL,
    long_url     TEXT NOT NULL,
    custom_alias VARCHAR(50),
    click_count  INT DEFAULT 0,
    expires_at   TIMESTAMPTZ,
    is_deleted   BOOLEAN DEFAULT FALSE,
    created_at   TIMESTAMPTZ NOT NULL,
    updated_at   TIMESTAMPTZ NOT NULL
);

CREATE UNIQUE INDEX idx_urls_short_code ON urls(short_code);
CREATE INDEX idx_urls_created_at ON urls(created_at DESC) WHERE is_deleted = FALSE;
```

**EF Core Global Filter:** `WHERE is_deleted = FALSE` — automatically applied to all queries.  
**Auth:** Azure Entra authentication only (password auth disabled in Bicep).

### Analytics DB (`analytics-db`)

```sql
CREATE TABLE click_records (
    id          UUID PRIMARY KEY,
    url_id      UUID NOT NULL,
    short_code  VARCHAR(50) NOT NULL,
    user_agent  TEXT,
    ip_address  VARCHAR(45),
    clicked_at  TIMESTAMPTZ NOT NULL
);

CREATE INDEX idx_clicks_short_code ON click_records(short_code);
CREATE INDEX idx_clicks_clicked_at ON click_records(clicked_at DESC);
CREATE INDEX idx_clicks_url_id ON click_records(url_id);
```

---

## 4. Service Bus Topics & Subscriptions

```
Azure Service Bus Namespace: sb-tinyurl-prod.servicebus.windows.net
│
├── Topic: url-events
│   └── Subscription: analytics-subscription
│       └── Consumer: (future — sync URL metadata to analytics DB)
│
└── Topic: click-events
    └── Subscription: analytics-click-subscription
        └── Consumer: Analytics Service ClickEventConsumer (BackgroundService)
            ├── MaxConcurrentCalls: 10
            ├── MaxDeliveryCount: 10
            └── DeadLetterOnExpiration: true
```

### Message Format

```json
// UrlClickedEvent
{
  "urlId": "guid",
  "shortCode": "abc1234",
  "userAgent": "Mozilla/5.0 ...",
  "ipAddress": "1.2.3.4",
  "clickedAt": "2025-01-01T12:00:00Z"
}
```

---

## 5. CQRS Map (URL Service)

| Operation | Type | Class | Publishes Event? |
|---|---|---|---|
| Create URL | Command | `CreateUrlHandler` | `UrlCreatedEvent` → Service Bus |
| Update URL | Command | `UpdateUrlHandler` | — (cache invalidation) |
| Delete URL | Command | `DeleteUrlHandler` | `UrlDeletedEvent` → Service Bus |
| List URLs | Query | `ListUrlsHandler` | — |
| Get URL by ID | Query | `GetUrlHandler` | — |

---

## 6. API Contracts

### URL Service — `https://api.tinyurl.example.com/api/urls`

| Method | Path | Description | Response |
|---|---|---|---|
| GET | `/api/urls` | Paginated list | 200 `UrlListResponseDto` |
| POST | `/api/urls` | Create short URL | 201 `ShortUrlDto` |
| GET | `/api/urls/{id}` | Get by ID | 200 `ShortUrlDto` |
| PATCH | `/api/urls/{id}` | Update URL/expiry | 200 `ShortUrlDto` |
| DELETE | `/api/urls/{id}` | Soft delete | 204 |

### Redirect Service

| Method | Path | Description | Response |
|---|---|---|---|
| GET | `/{shortCode}` | Resolve + redirect | 302 / 404 / 410 |

### Analytics Service — `/api/analytics`

| Method | Path | Description |
|---|---|---|
| GET | `/api/analytics/summary` | Total clicks, today's clicks, top 5 |
| GET | `/api/analytics/clicks/{code}` | Time-series by short code |
| GET | `/api/analytics/top` | Top URLs by click count |

---

## 7. CI/CD Pipeline Stages

```
Each service has its own independent pipeline:

[Code Push]
    │
    ▼
Stage 1: BUILD
  ├── Restore packages
  ├── Build (Release)
  ├── Run unit tests + code coverage
  └── SAST security scan

    │ (on main branch only)
    ▼
Stage 2: CONTAINERIZE
  ├── Login to ACR (Managed Identity)
  ├── Docker build
  ├── Trivy scan (block on HIGH/CRITICAL)
  └── Push to ACR

    │
    ▼
Stage 3: DEPLOY DEV
  ├── Get AKS credentials
  ├── Apply K8s manifests (rolling update)
  └── Verify rollout health

    │ (approval gate)
    ▼
Stage 4: DEPLOY PROD
  ├── Get AKS credentials (prod)
  ├── Blue-Green deployment
  └── Verify rollout health
```

---

## 8. Zero Trust Implementation Details

### Workload Identity (No Secrets in Code)

```yaml
# Pod annotation
azure.workload.identity/use: "true"

# Service Account
metadata:
  annotations:
    azure.workload.identity/client-id: <USER_ASSIGNED_MI_CLIENT_ID>
```

```csharp
// DefaultAzureCredential — automatically picks up Workload Identity in AKS
var credential = new DefaultAzureCredential();
var serviceBusClient = new ServiceBusClient("sb-tinyurl.servicebus.windows.net", credential);
```

### Network Policy (Deny All Default)

```yaml
# Default deny ALL traffic in namespace
kind: NetworkPolicy
spec:
  podSelector: {}
  policyTypes: [Ingress, Egress]
  # No rules = deny everything
```

### Redis TLS Configuration

```
enableNonSslPort: false       # Port 6379 disabled
minimumTlsVersion: '1.2'     # Only TLS 1.2+
publicNetworkAccess: Disabled # Private endpoint only
```

### PostgreSQL Entra Auth Only

```
authConfig:
  activeDirectoryAuth: Enabled
  passwordAuth: Disabled       # No password login possible
```

---

## 9. OpenTelemetry Instrumentation

```csharp
// ServiceDefaults registers automatically for ALL services:
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()   // HTTP request metrics
        .AddHttpClientInstrumentation()   // Outbound HTTP metrics
        .AddRuntimeInstrumentation())     // GC, thread pool, heap
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()   // Automatic span per request
        .AddHttpClientInstrumentation()   // Outbound HTTP spans
        .AddSource("TinyUrl.*"));         // Custom business spans

// Exporter: OTLP → Aspire Dashboard (local) or Azure Monitor (production)
```

---

## 10. HPA Scaling Targets

| Service | Min | Max | CPU Target | Scale Reason |
|---|---|---|---|---|
| URL Service | 3 | 20 | 70% | Write workload |
| Redirect Service | 5 | 50 | 60% | Highest traffic — redirect heavy |
| Analytics Service | 2 | 10 | 70% | Background consumer, lower load |
