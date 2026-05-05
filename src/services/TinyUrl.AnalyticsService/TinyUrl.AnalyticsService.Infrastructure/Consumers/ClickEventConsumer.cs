using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using TinyUrl.AnalyticsService.Infrastructure.Data;
using TinyUrl.Contracts.Events;

namespace TinyUrl.AnalyticsService.Infrastructure.Consumers;

public class ClickEventConsumer(
    ServiceBusClient client,
    IServiceScopeFactory scopeFactory,
    ILogger<ClickEventConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var processor = client.CreateProcessor("click-events", "analytics-click-subscription",
            new ServiceBusProcessorOptions { MaxConcurrentCalls = 10 });

        processor.ProcessMessageAsync += HandleMessageAsync;
        processor.ProcessErrorAsync += HandleErrorAsync;

        await processor.StartProcessingAsync(ct);
        await Task.Delay(Timeout.Infinite, ct);
        await processor.StopProcessingAsync(ct);
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        try
        {
            var @event = JsonSerializer.Deserialize<UrlClickedEvent>(args.Message.Body.ToString());
            if (@event is null) { await args.DeadLetterMessageAsync(args.Message); return; }

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();

            db.ClickRecords.Add(new ClickRecord
            {
                Id = Guid.NewGuid(),
                UrlId = @event.UrlId,
                ShortCode = @event.ShortCode,
                UserAgent = @event.UserAgent,
                IpAddress = @event.IpAddress,
                ClickedAt = @event.ClickedAt
            });

            await db.SaveChangesAsync();
            await args.CompleteMessageAsync(args.Message);
            logger.LogInformation("Recorded click for {ShortCode}", @event.ShortCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process click event");
            await args.AbandonMessageAsync(args.Message);
        }
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        logger.LogError(args.Exception, "Service Bus error: {Source}", args.ErrorSource);
        return Task.CompletedTask;
    }
}
