# TinyURL Gateway — Azure Microservices

A cloud-native, production-grade URL shortening platform built on **Azure + ASP.NET Core 9 + .NET 9**.

## Architecture

```
Azure Front Door → Azure API Management → AKS (3 microservices)
                                         ├── URL Service      (CRUD)
                                         ├── Redirect Service (high-throughput)
                                         └── Analytics Service (event-driven)
                                              ↕ Azure Service Bus (events)
                                         Azure DB for PostgreSQL (per-service DB)
                                         Azure Cache for Redis (shared cache)
                                         Azure Key Vault (all secrets via Managed Identity)
                                         Azure Monitor + App Insights (full observability)
```

## Key Features

- **Zero Trust Security** — Workload Identity, no passwords, private endpoints, deny-all network policies
- **Centralized Observability** — OpenTelemetry → Azure Monitor + Application Insights + .NET Aspire Dashboard
- **Event-Driven** — Azure Service Bus topics for URL and click events
- **.NET Aspire** — Full local orchestration with dashboard, service discovery, and emulated Azure resources
- **15 Design Patterns** — CQRS, Mediator, Cache-Aside, Circuit Breaker, Publisher-Subscriber, Blue-Green, and more
- **Azure DevOps CI/CD** — Separate pipeline per microservice with SAST, Trivy, and blue-green production deploy

## Quick Start (Local with .NET Aspire)

```bash
# Prerequisites: .NET 9 SDK, Docker Desktop, Azure CLI
dotnet run --project src/TinyUrl.AppHost

# Aspire Dashboard: http://localhost:18888
# URL Service:       http://localhost:5001/swagger
# Redirect Service:  http://localhost:5002
# Analytics Service: http://localhost:5003/swagger
```

## Deploy to Azure

```bash
# 1. Deploy infrastructure
az deployment sub create \
  --location eastus \
  --template-file deploy/bicep/main.bicep \
  --parameters environment=dev

# 2. Set up Azure DevOps pipelines
# Import each file from pipelines/ into Azure DevOps

# 3. Configure variable group: tinyurl-azure-vars
# Variables needed: azureServiceConnection, acrName, aksClusterName,
#                   resourceGroup, azureLocation, acrServiceConnection
```

## Design Documentation

| Document | Contents |
|---|---|
| [design/HLD.md](design/HLD.md) | System architecture, Azure services map, Zero Trust model, observability pipeline |
| [design/LLD.md](design/LLD.md) | Project structure, DB schema, API contracts, CI/CD stages, HPA targets |
| [design/DesignPatterns.md](design/DesignPatterns.md) | All 15 patterns with code examples |

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 9 / C# 13 |
| Framework | ASP.NET Core 9 |
| Orchestration | .NET Aspire 9 |
| ORM | Entity Framework Core 9 |
| Database | Azure DB for PostgreSQL 16 (Flexible Server) |
| Cache | Azure Cache for Redis (Premium) |
| Messaging | Azure Service Bus (Premium) |
| Secrets | Azure Key Vault + Workload Identity |
| Containers | AKS + ACR (Premium) |
| Observability | OpenTelemetry + Azure Monitor + App Insights |
| IaC | Azure Bicep |
| CI/CD | Azure DevOps Pipelines |
| Security | Zero Trust, Trivy, SAST, Defender for Containers |
