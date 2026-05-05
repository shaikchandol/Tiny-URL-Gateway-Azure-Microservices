using Azure.Messaging.ServiceBus;
using System.Text.Json;
using TinyUrl.UrlService.Application.Interfaces;

namespace TinyUrl.UrlService.Infrastructure.Services;

public class ServiceBusEventPublisher(ServiceBusClient client) : IEventPublisher
{
    public async Task PublishAsync<T>(string topicName, T @event, CancellationToken ct = default)
    {
        await using var sender = client.CreateSender(topicName);
        var json = JsonSerializer.Serialize(@event);
        var message = new ServiceBusMessage(json) { ContentType = "application/json", Subject = typeof(T).Name };
        await sender.SendMessageAsync(message, ct);
    }
}

public class Base62ShortCodeGenerator : IShortCodeGenerator
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private static readonly Random _rng = new();
    public string Generate(int length = 7) =>
        new(Enumerable.Range(0, length).Select(_ => Alphabet[_rng.Next(Alphabet.Length)]).ToArray());
}

public class RedisCacheService(Microsoft.Extensions.Caching.Distributed.IDistributedCache cache) : ICacheService
{
    public Task<string?> GetAsync(string key, CancellationToken ct = default) => cache.GetStringAsync(key, ct);

    public Task SetAsync(string key, string value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var opts = new Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions();
        if (expiry.HasValue) opts.AbsoluteExpirationRelativeToNow = expiry;
        return cache.SetStringAsync(key, value, opts, ct);
    }

    public Task RemoveAsync(string key, CancellationToken ct = default) => cache.RemoveAsync(key, ct);
}
