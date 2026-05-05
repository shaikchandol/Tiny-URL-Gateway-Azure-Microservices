# Design Patterns — TinyURL Azure Microservices

## Overview

This document details the 15 design patterns applied across the TinyURL Azure Microservices platform, covering architectural, behavioral, structural, cloud-native, and security patterns.

---

## 1. Microservices Pattern

**Category:** Architectural  
**Scope:** Entire platform

The system is decomposed into three independently deployable services, each owning its own database, scaling profile, and deployment pipeline.

```
URL Service          Redirect Service      Analytics Service
     │                      │                      │
  urlservice-db         Redis (primary)       analytics-db
  Redis (write)         URL Svc (fallback)    Service Bus (consume)
  Service Bus (pub)     Service Bus (pub)
```

**Benefits:**
- Independent scaling — Redirect Service scales to 50 replicas; Analytics stays at 2–10
- Independent deployment — deploy one service without touching others
- Fault isolation — Analytics Service failure doesn't affect redirects
- Technology freedom — each service can evolve independently

---

## 2. CQRS — Command Query Responsibility Segregation

**Category:** Architectural  
**Location:** `TinyUrl.UrlService.Application/Commands/`, `Queries/`

Commands (state-changing) and Queries (read-only) are separated into distinct classes and handlers.

```
COMMANDS (Write Path):          QUERIES (Read Path):
CreateUrlCommand                ListUrlsQuery
UpdateUrlCommand                GetUrlQuery
DeleteUrlCommand
     │                               │
     ▼                               ▼
PostgreSQL (write)            PostgreSQL (read)
Redis (write/invalidate)
Service Bus (publish)
```

**Benefits:** Independent optimization of read and write paths. Queries can be cached; commands publish events.

---

## 3. Mediator Pattern

**Category:** Behavioral  
**Library:** MediatR 12  
**Location:** All controllers dispatch via `IMediator.Send()`

```csharp
// Controller — no dependency on handler
await _mediator.Send(new CreateUrlCommand(longUrl, alias, expiry));

// Handler — resolved by MediatR, never directly called
public class CreateUrlHandler : IRequestHandler<CreateUrlCommand, ShortUrlDto>
```

**Benefits:** Controllers are thin. New operations added without changing existing controllers. Pipeline behaviors apply to all commands transparently.

---

## 4. Repository Pattern

**Category:** Structural  
**Location:** `IUrlRepository` ↔ `UrlRepository` (EF Core)

```csharp
// Application layer — no EF Core reference
public interface IUrlRepository {
    Task<ShortUrl?> GetByShortCodeAsync(string code, CancellationToken ct);
    Task<ShortUrl> AddAsync(ShortUrl url, CancellationToken ct);
    Task<bool> ShortCodeExistsAsync(string code, CancellationToken ct);
}

// Infrastructure layer — full EF Core implementation
public class UrlRepository(UrlServiceDbContext db) : IUrlRepository { ... }
```

**Benefits:** Domain and Application layers have zero coupling to EF Core or PostgreSQL.

---

## 5. Cache-Aside Pattern

**Category:** Cloud / Performance  
**Location:** Redirect Service + URL Service (Redis)

```
READ (Redirect):
  1. Redis.Get("url:{code}")
     ├─ HIT  → Return longUrl (< 1ms, zero DB calls)
     └─ MISS → Call URL Service HTTP API
               → Redis.Set("url:{code}", TTL=24h)
               → Return longUrl

WRITE (Create):
  URL Service creates URL → Redis.Set pre-warms cache

INVALIDATE (Update / Delete):
  URL Service → Redis.Remove("url:{code}")
```

**Benefits:** Sub-millisecond redirect on cache hit. Redis absorbs 95%+ of read traffic, protecting PostgreSQL.

---

## 6. Event-Driven / Publisher-Subscriber Pattern

**Category:** Architectural / Messaging  
**Location:** URL Service (publisher) → Service Bus → Analytics Service (subscriber)

```
URL Service                    Azure Service Bus              Analytics Service
     │                              │                              │
     │── UrlCreatedEvent ──────────►│── url-events ──────────────►│
     │── UrlDeletedEvent ──────────►│── url-events                │
     │                              │                              │
Redirect Service                   │                              │
     │── UrlClickedEvent ──────────►│── click-events ────────────►│── INSERT click_records
```

**Benefits:**
- Services are temporally decoupled — Analytics Service can be down and messages queue up
- Dead-letter queue catches processing failures
- New subscribers can be added without changing publishers

---

## 7. Sidecar / Service Defaults Pattern (Aspire)

**Category:** Cloud-Native  
**Location:** `TinyUrl.ServiceDefaults/Extensions.cs`

All services call `builder.AddServiceDefaults()` to inherit:
- OpenTelemetry tracing, metrics, logs
- Service discovery
- Standard resilience handler for HTTP clients
- Health check endpoints (`/health`, `/health/ready`, `/health/live`)

```csharp
// Every service's Program.cs — one line adds everything
builder.AddServiceDefaults();
```

**Benefits:** Consistent observability and resilience across all services with no code duplication.

---

## 8. Circuit Breaker Pattern

**Category:** Resilience  
**Library:** Standard Resilience Handler (Microsoft.Extensions.Http.Resilience)

Applied automatically to all inter-service HTTP calls:

```csharp
// ServiceDefaults configures for ALL HttpClient instances
builder.Services.ConfigureHttpClientDefaults(http => {
    http.AddStandardResilienceHandler(); // Retry + Circuit Breaker + Timeout
});
```

```
States:
CLOSED → Normal (requests pass through)
  │ (5 failures in 30s)
  ▼
OPEN → Fast fail (no DB/network calls)
  │ (30s timeout)
  ▼
HALF-OPEN → One test request
  ├── Success → CLOSED
  └── Failure → OPEN
```

**Benefits:** Redirect Service won't hammer a degraded URL Service. Fail fast gives better UX than waiting for timeouts.

---

## 9. Factory Method Pattern

**Category:** Creational  
**Location:** `ShortUrl.Create()` domain entity

```csharp
private ShortUrl() { }  // No direct construction

public static ShortUrl Create(string shortCode, string longUrl,
    string? customAlias = null, DateTimeOffset? expiresAt = null)
{
    return new ShortUrl {
        Id = Guid.NewGuid(),       // Always new ID
        ClickCount = 0,            // Always starts at 0
        IsDeleted = false,         // Always starts active
        CreatedAt = DateTimeOffset.UtcNow,  // Always stamped
        // ...
    };
}
```

**Benefits:** Entity invariants guaranteed at creation — impossible to create an invalid ShortUrl.

---

## 10. Strategy Pattern

**Category:** Behavioral  
**Location:** `IShortCodeGenerator` ↔ `Base62ShortCodeGenerator`

```csharp
public interface IShortCodeGenerator { string Generate(int length = 7); }

// Default: Base62 (0-9, A-Z, a-z) = 62^7 ≈ 3.5 trillion combos
public class Base62ShortCodeGenerator : IShortCodeGenerator { ... }

// Swappable via DI — NanoID, sequential, hash-based
services.AddSingleton<IShortCodeGenerator, Base62ShortCodeGenerator>();
```

**Benefits:** Short code algorithm swappable without touching business logic.

---

## 11. Pipeline Behavior Pattern (Decorator for MediatR)

**Category:** Behavioral  
**Location:** `LoggingBehavior`, `ValidationBehavior`

```
Request
  │
  ▼
LoggingBehavior ── logs request name
  │
  ▼
ValidationBehavior ── runs FluentValidation; throws on failure
  │
  ▼
Handler ── actual business logic
  │
  ▼
Response
```

**Benefits:** Cross-cutting concerns (logging, validation) applied to ALL commands and queries with zero handler changes.

---

## 12. Outbox Pattern (via Azure Service Bus)

**Category:** Messaging Reliability  
**Location:** URL Service → Service Bus Premium

Service Bus Premium with duplicate detection acts as a reliable outbox:

```
1. PostgreSQL INSERT (url row)
2. Service Bus SEND (UrlCreatedEvent)

If step 2 fails:
  → Polly retry with exponential backoff
  → Message delivered at-least-once (idempotent consumers required)

Service Bus guarantees:
  ├── At-least-once delivery
  ├── Dead-letter after 10 retries
  └── Duplicate detection (MessageId deduplication)
```

**Benefits:** Events are not lost even if the Analytics Service is temporarily down.

---

## 13. Workload Identity / Zero Trust Identity Pattern

**Category:** Security  
**Location:** AKS pod → Azure services (Key Vault, Service Bus, PostgreSQL, Redis)

```
Pod (url-service)
  │ uses Kubernetes Service Account
  │ bound to Azure Managed Identity
  │ via OIDC federation
  ▼
Azure Entra ID issues token
  ▼
Token presented to Azure Service Bus
  ▼
Access granted (no passwords, no connection strings)
```

```csharp
// DefaultAzureCredential — zero config, works in AKS automatically
var credential = new DefaultAzureCredential();
var client = new ServiceBusClient("sb.servicebus.windows.net", credential);
```

**Benefits:** No secrets anywhere in code, env vars, or K8s secrets. Full Zero Trust identity.

---

## 14. Infrastructure as Code Pattern (Bicep)

**Category:** DevOps / Cloud  
**Location:** `deploy/bicep/`

All Azure infrastructure defined as parameterized Bicep templates:

```bicep
// main.bicep deploys entire environment
module aks 'modules/aks.bicep' = {
  params: {
    clusterName: '${aksClusterName}-${environment}'
    // Workload Identity, Defender, Azure Policy — all enabled
  }
}

module postgres 'modules/postgres.bicep' = {
  params: {
    // Entra Auth only — passwordAuth: Disabled
    // Zone redundant
    // Backups to geo-redundant storage
  }
}
```

**Benefits:** Environments (dev/prod) created identically. Infrastructure reviewed in PRs. Validated in CI before deployment.

---

## 15. Blue-Green Deployment Pattern

**Category:** Deployment  
**Location:** Azure DevOps Production pipeline (`strategy: blueGreen`)

```
Production Deploy:
  [Blue] Current running version (100% traffic)
       │
       │ Deploy [Green] (new version)
       │
  [Blue] 100% traffic  [Green] 0% traffic (warming up)
       │
       │ Health checks pass
       │
  [Blue] 0% traffic   [Green] 100% traffic
       │
       │ Observe for N minutes
       │
       ├─ Success → Remove Blue
       └─ Failure → Rollback (swap traffic back to Blue instantly)
```

**Benefits:** Zero-downtime deployments. Instant rollback if new version has issues.

---

## Summary

| # | Pattern | Category | Applied In |
|---|---|---|---|
| 1 | Microservices | Architectural | Entire platform |
| 2 | CQRS | Architectural | URL Service |
| 3 | Mediator | Behavioral | URL Service |
| 4 | Repository | Structural | URL Service |
| 5 | Cache-Aside | Cloud/Performance | Redirect + URL Service |
| 6 | Publisher-Subscriber | Messaging | All services → Service Bus |
| 7 | Service Defaults (Sidecar) | Cloud-Native | All services (Aspire) |
| 8 | Circuit Breaker | Resilience | Redirect → URL Service |
| 9 | Factory Method | Creational | Domain entities |
| 10 | Strategy | Behavioral | Short code generation |
| 11 | Pipeline Behavior | Behavioral | URL Service MediatR |
| 12 | Outbox (via Service Bus) | Messaging Reliability | URL Service → Analytics |
| 13 | Workload Identity | Security/Zero Trust | All AKS services |
| 14 | Infrastructure as Code | DevOps | Bicep templates |
| 15 | Blue-Green Deployment | Deployment | Azure DevOps prod pipelines |
