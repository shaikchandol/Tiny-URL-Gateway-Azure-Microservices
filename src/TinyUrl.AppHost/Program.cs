using Aspire.Hosting.Azure;

var builder = DistributedApplication.CreateBuilder(args);

// ─── Azure Infrastructure ────────────────────────────────────────────────────

var keyVault = builder.AddAzureKeyVault("tinyurl-keyvault");

var redis = builder.AddAzureRedis("tinyurl-redis");

var postgres = builder.AddAzurePostgresFlexibleServer("tinyurl-postgres");
var urlDb = postgres.AddDatabase("urlservice-db");
var analyticsDb = postgres.AddDatabase("analytics-db");

var serviceBus = builder.AddAzureServiceBus("tinyurl-servicebus")
    .AddTopic("url-events", ["analytics-subscription"])
    .AddTopic("click-events", ["analytics-click-subscription"]);

var appInsights = builder.AddAzureApplicationInsights("tinyurl-insights");

// ─── Microservices ────────────────────────────────────────────────────────────

var urlService = builder.AddProject<Projects.TinyUrl_UrlService_Api>("url-service")
    .WithReference(urlDb)
    .WithReference(redis)
    .WithReference(serviceBus)
    .WithReference(keyVault)
    .WithReference(appInsights)
    .WithHttpHealthCheck("/health");

var redirectService = builder.AddProject<Projects.TinyUrl_RedirectService_Api>("redirect-service")
    .WithReference(redis)
    .WithReference(serviceBus)
    .WithReference(keyVault)
    .WithReference(appInsights)
    .WithHttpHealthCheck("/health");

var analyticsService = builder.AddProject<Projects.TinyUrl_AnalyticsService_Api>("analytics-service")
    .WithReference(analyticsDb)
    .WithReference(serviceBus)
    .WithReference(keyVault)
    .WithReference(appInsights)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
