# High-Level Design (HLD) — TinyURL Azure Microservices

## 1. System Overview

TinyURL Azure Microservices is a cloud-native, production-grade URL shortening platform built on Azure. It follows the microservices pattern with each service independently deployable, independently scalable, and backed by dedicated Azure infrastructure. All communication uses Managed Identity — no passwords or connection strings in code.

---

## 2. Architecture Diagram

```
┌──────────────────────────────────────────────────────────────────────────────────┐
│                            CLIENTS                                               │
│  ┌──────────────┐   ┌──────────────┐   ┌──────────────┐   ┌──────────────────┐  │
│  │  React App   │   │  Mobile App  │   │  CLI / REST  │   │  Partner Systems │  │
│  └──────┬───────┘   └──────┬───────┘   └──────┬───────┘   └────────┬─────────┘  │
└─────────┼──────────────────┼──────────────────┼────────────────────┼────────────┘
          │                  │                  │                    │
          ▼                  ▼                  ▼                    ▼
┌──────────────────────────────────────────────────────────────────────────────────┐
│                    AZURE FRONT DOOR + WAF                                        │
│              (Global CDN, DDoS Protection, TLS Termination)                      │
└────────────────────────────────────┬─────────────────────────────────────────────┘
                                     │
                                     ▼
┌──────────────────────────────────────────────────────────────────────────────────┐
│              AZURE API MANAGEMENT (APIM)                                         │
│   Rate Limiting │ OAuth 2.0 / AAD │ IP Filtering │ Request Transform │ Logging  │
└──────────────────────────────┬─────────────────────────────────────────────────-─┘
                               │
          ┌────────────────────┼──────────────────────┐
          │                    │                       │
          ▼                    ▼                       ▼
┌──────────────────┐  ┌─────────────────┐  ┌──────────────────────┐
│   URL SERVICE    │  │ REDIRECT SERVICE │  │  ANALYTICS SERVICE   │
│                  │  │                 │  │                      │
│ ASP.NET Core 9   │  │ ASP.NET Core 9  │  │  ASP.NET Core 9      │
│ CQRS + MediatR   │  │ Cache-Aside     │  │  Service Bus Consumer│
│ EF Core 9        │  │ Redis-first     │  │  Time-series queries │
│                  │  │ Service Bus pub │  │                      │
│ /api/urls CRUD   │  │ /{code} → 302   │  │  /api/analytics      │
└───────┬──────────┘  └───────┬─────────┘  └──────────┬───────────┘
        │                     │                        │
        ▼                     │                        ▼
┌─────────────────┐           │            ┌──────────────────────┐
│  Azure DB for   │           │            │  Azure DB for        │
│  PostgreSQL 16  │           │            │  PostgreSQL 16       │
│  (Flexible Svr) │           │            │  (Analytics DB)      │
│  Zone Redundant │           │            │  Zone Redundant      │
└─────────────────┘           │            └──────────────────────┘
                              │
                              ▼
                   ┌────────────────────┐
                   │  Azure Cache for   │
                   │  Redis (Premium)   │
                   │  Zone Redundant    │
                   │  TLS only          │
                   └──────────┬─────────┘
                              │
                              ▼
                   ┌──────────────────────┐
                   │  Azure Service Bus   │
                   │  (Premium, ZR)       │
                   │  Topics: url-events  │
                   │          click-events│
                   └──────────────────────┘

─── AZURE PLATFORM SERVICES ──────────────────────────────────────────────────────
 ┌────────────────┐ ┌───────────────────┐ ┌──────────────────┐ ┌──────────────┐
 │ Azure Key Vault│ │ Azure Monitor +   │ │ Azure Container  │ │ Azure Policy │
 │ (all secrets)  │ │ App Insights      │ │ Registry (ACR)   │ │ + Defender   │
 │ RBAC only      │ │ Distributed trace │ │ Premium, ZR      │ │ for Containers│
 └────────────────┘ └───────────────────┘ └──────────────────┘ └──────────────┘

 ┌────────────────────────────────────────────────────────────────────────────────┐
 │             AZURE KUBERNETES SERVICE (AKS)                                     │
 │   Workload Identity │ Azure CNI │ Network Policies │ AGIC │ Azure Policy       │
 │   Microsoft Defender for Containers │ OMS Agent │ KeyVault CSI Driver          │
 └────────────────────────────────────────────────────────────────────────────────┘

 ┌─────────────────────────────────────────────────────────────────────────────────┐
 │                 .NET ASPIRE DASHBOARD                                           │
 │     Service Discovery │ Telemetry │ Logs │ Metrics │ Distributed Traces         │
 └─────────────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Microservices

### 3.1 URL Service
- **Responsibility:** CRUD operations for short URLs
- **Pattern:** Clean Architecture + CQRS + MediatR
- **Database:** Azure DB for PostgreSQL Flexible Server (dedicated `urlservice-db`)
- **Cache:** Azure Cache for Redis (write-through on create, invalidate on update/delete)
- **Events:** Publishes `UrlCreatedEvent`, `UrlDeletedEvent` to Azure Service Bus
- **Auth:** Azure Workload Identity (Managed Identity) for all Azure resources

### 3.2 Redirect Service
- **Responsibility:** High-throughput short code resolution and redirect
- **Pattern:** Cache-Aside with Redis as primary, URL Service as fallback
- **Cache:** Azure Cache for Redis (sub-ms resolution on HIT)
- **Events:** Publishes `UrlClickedEvent` to Azure Service Bus (fire-and-forget)
- **Resilience:** Polly retry + circuit breaker, Standard Resilience Handler
- **Scale:** 5–50 replicas (HPA), highest-traffic service

### 3.3 Analytics Service
- **Responsibility:** Click event processing and analytics queries
- **Pattern:** Event consumer (Service Bus background worker)
- **Database:** Azure DB for PostgreSQL (dedicated `analytics-db`)
- **Events:** Consumes `UrlClickedEvent` from Service Bus topic subscription
- **Dead Letter:** Failed messages auto-moved to DLQ after 10 retries

---

## 4. Azure Services Map

| Azure Service | Purpose | SKU |
|---|---|---|
| Azure Kubernetes Service (AKS) | Container orchestration | Standard/Premium |
| Azure Container Registry (ACR) | Container image storage | Premium |
| Azure DB for PostgreSQL Flexible | Per-service databases | General Purpose D4s |
| Azure Cache for Redis | Distributed cache + redirect store | Premium P1 |
| Azure Service Bus | Async event messaging | Premium (ZR) |
| Azure Key Vault | Secrets, TLS certs | Standard |
| Azure Monitor + App Insights | Centralized telemetry | — |
| Azure Front Door | Global CDN + DDoS + WAF | Premium |
| Azure API Management | API gateway, rate limiting | Standard V2 |
| Azure Policy | Governance and compliance | Built-in |
| Microsoft Defender for Containers | Runtime threat detection | — |

---

## 5. Zero Trust Security Model

```
NEVER TRUST, ALWAYS VERIFY

┌──────────────────────────────────────────────────────────────────────┐
│                        ZERO TRUST PILLARS                            │
├──────────────────────────────────────────────────────────────────────┤
│ IDENTITY    │ Azure Entra ID + Workload Identity (no passwords)      │
│             │ Managed Identity for all service-to-Azure comms        │
│             │ RBAC — least privilege on all resources                │
├──────────────────────────────────────────────────────────────────────┤
│ NETWORK     │ Private endpoints for all Azure PaaS services          │
│             │ Azure CNI + Network Policies (deny-all default)        │
│             │ mTLS between pods (Azure Service Mesh)                 │
│             │ Azure Front Door WAF (OWASP ruleset)                   │
├──────────────────────────────────────────────────────────────────────┤
│ DATA        │ Encryption at rest (Azure-managed keys)                │
│             │ TLS 1.2+ enforced everywhere                           │
│             │ Key Vault — no secrets in env vars or code             │
│             │ PostgreSQL: Entra Auth only (password auth disabled)   │
│             │ Service Bus: local auth disabled (SAS keys disabled)   │
│             │ Redis: TLS only, no non-SSL port                       │
├──────────────────────────────────────────────────────────────────────┤
│ WORKLOADS   │ AKS: non-root containers, read-only root filesystem    │
│             │ Drop all Linux capabilities                            │
│             │ Pod Security Standards enforced via Azure Policy       │
│             │ Microsoft Defender for Containers (runtime threat)     │
│             │ Trivy image scanning in CI/CD (block CRITICAL/HIGH)    │
│             │ ACR: content trust (Notary), no admin user             │
└──────────────────────────────────────────────────────────────────────┘
```

---

## 6. Centralized Observability Stack

```
┌──────────────────────────────────────────────────────────────────────┐
│               OBSERVABILITY PIPELINE                                 │
│                                                                      │
│  [Each microservice]                                                 │
│       │                                                              │
│       │ OpenTelemetry SDK (Traces + Metrics + Logs)                  │
│       ▼                                                              │
│  OTLP Exporter ──────► .NET Aspire Dashboard (local dev)            │
│       │                                                              │
│       └──────────────► Azure Monitor OTLP Endpoint                  │
│                              │                                       │
│                    ┌─────────┴──────────────┐                        │
│                    ▼                        ▼                        │
│           Application Insights         Log Analytics                 │
│        (Distributed Tracing)         Workspace (Logs)               │
│              │                              │                        │
│              └──────────────────────────────┘                        │
│                              │                                       │
│                    Azure Monitor Dashboards                          │
│                    Azure Monitor Alerts                              │
│                    Smart Detection                                   │
└──────────────────────────────────────────────────────────────────────┘

Instrumented:
  - All HTTP requests (ASP.NET Core instrumentation)
  - All outbound HTTP calls (HttpClient instrumentation)
  - Service Bus publish/consume
  - Database queries (EF Core)
  - Redis operations
  - Custom business spans (TinyUrl.* activity source)
```

---

## 7. .NET Aspire Dashboard

The AppHost project orchestrates all services locally with full Aspire support:

```
dotnet run --project src/TinyUrl.AppHost

┌────────────────────────────────────────────────┐
│          .NET ASPIRE DASHBOARD                  │
│  http://localhost:18888                         │
│                                                 │
│  Services:                                      │
│  ● url-service      :5001  RUNNING              │
│  ● redirect-service :5002  RUNNING              │
│  ● analytics-service:5003  RUNNING              │
│                                                 │
│  Resources:                                     │
│  ● tinyurl-redis        RUNNING                 │
│  ● tinyurl-postgres     RUNNING                 │
│  ● tinyurl-servicebus   EMULATED                │
│                                                 │
│  Telemetry: Traces │ Metrics │ Logs             │
│  Distributed trace waterfall across services    │
└────────────────────────────────────────────────┘
```

---

## 8. Data Flow Diagrams

### 8.1 Create Short URL
```
Client → APIM → URL Service
  → ValidationBehavior (FluentValidation)
  → CreateUrlHandler
    → Generate Base62 code
    → PostgreSQL INSERT (urlservice-db)
    → Redis SET ("url:{code}", TTL=24h)
    → Service Bus PUBLISH (url-events topic)
  → 201 Created
```

### 8.2 Redirect
```
Client → APIM → Redirect Service
  → Redis GET "url:{code}"
    ├─ HIT  → Service Bus PUBLISH click-events → 302
    └─ MISS → URL Service HTTP GET /resolve/{code}
              → Redis SET "url:{code}"
              → Service Bus PUBLISH click-events
              → 302 Redirect
```

### 8.3 Click Analytics
```
Service Bus "click-events" topic
  → Analytics Service (BackgroundService consumer)
    → PostgreSQL INSERT click_records (analytics-db)
    → Ack message
    [on failure → retry 10x → Dead Letter Queue]

Client → APIM → Analytics Service
  → /api/analytics/summary
  → /api/analytics/clicks/{code}
  → /api/analytics/top
```
