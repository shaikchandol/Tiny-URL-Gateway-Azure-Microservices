using TinyUrl.UrlService.Domain.Entities;

namespace TinyUrl.UrlService.Application.Interfaces;

public interface IUrlRepository
{
    Task<ShortUrl?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ShortUrl?> GetByShortCodeAsync(string shortCode, CancellationToken ct = default);
    Task<(IEnumerable<ShortUrl> Urls, int Total)> ListAsync(int page, int limit, string? search, CancellationToken ct = default);
    Task<ShortUrl> AddAsync(ShortUrl url, CancellationToken ct = default);
    Task UpdateAsync(ShortUrl url, CancellationToken ct = default);
    Task<bool> ShortCodeExistsAsync(string shortCode, CancellationToken ct = default);
}

public interface IEventPublisher
{
    Task PublishAsync<T>(string topicName, T @event, CancellationToken ct = default);
}

public interface IShortCodeGenerator
{
    string Generate(int length = 7);
}

public interface ICacheService
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string value, TimeSpan? expiry = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
}
