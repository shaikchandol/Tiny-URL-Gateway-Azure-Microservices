using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using TinyUrl.Contracts.Events;

namespace TinyUrl.RedirectService.Api.Controllers;

[ApiController]
public class RedirectController(
    IDistributedCache cache,
    ServiceBusClient serviceBusClient,
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<RedirectController> logger) : ControllerBase
{
    [HttpGet("/{shortCode}")]
    public async Task<IActionResult> Redirect(string shortCode, CancellationToken ct = default)
    {
        if (shortCode is "health" or "swagger" or "api") return NotFound();

        var cacheKey = $"url:{shortCode}";
        var cached = await cache.GetStringAsync(cacheKey, ct);

        if (cached is not null)
        {
            logger.LogInformation("Cache HIT for {ShortCode}", shortCode);
            await PublishClickAsync(shortCode, ct);
            return RedirectPermanent(cached);
        }

        logger.LogInformation("Cache MISS for {ShortCode} — querying URL Service", shortCode);

        var urlServiceBase = config["services__url-service__https__0"] ?? "http://url-service";
        var client = httpClientFactory.CreateClient("url-service");

        var response = await client.GetAsync($"{urlServiceBase}/api/urls/resolve/{shortCode}", ct);
        if (!response.IsSuccessStatusCode) return NotFound();

        var json = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonSerializer.Deserialize<JsonElement>(json);
        var longUrl = doc.GetProperty("longUrl").GetString();
        if (string.IsNullOrWhiteSpace(longUrl)) return NotFound();

        await cache.SetStringAsync(cacheKey, longUrl, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
        }, ct);

        await PublishClickAsync(shortCode, ct);
        return RedirectPermanent(longUrl);
    }

    private async Task PublishClickAsync(string shortCode, CancellationToken ct)
    {
        try
        {
            await using var sender = serviceBusClient.CreateSender("click-events");
            var @event = new UrlClickedEvent(Guid.Empty, shortCode,
                Request.Headers.UserAgent.ToString(), HttpContext.Connection.RemoteIpAddress?.ToString(), DateTimeOffset.UtcNow);
            var message = new ServiceBusMessage(JsonSerializer.Serialize(@event))
            { ContentType = "application/json", Subject = nameof(UrlClickedEvent) };
            await sender.SendMessageAsync(message, ct);
        }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to publish click event for {ShortCode}", shortCode); }
    }
}
