using MediatR;
using TinyUrl.Contracts.Events;
using TinyUrl.UrlService.Application.DTOs;
using TinyUrl.UrlService.Application.Interfaces;
using TinyUrl.UrlService.Domain.Entities;

namespace TinyUrl.UrlService.Application.Commands.CreateUrl;

public class CreateUrlHandler : IRequestHandler<CreateUrlCommand, ShortUrlDto>
{
    private readonly IUrlRepository _repo;
    private readonly IShortCodeGenerator _gen;
    private readonly IEventPublisher _publisher;
    private readonly ICacheService _cache;

    public CreateUrlHandler(IUrlRepository repo, IShortCodeGenerator gen, IEventPublisher publisher, ICacheService cache)
    {
        _repo = repo; _gen = gen; _publisher = publisher; _cache = cache;
    }

    public async Task<ShortUrlDto> Handle(CreateUrlCommand request, CancellationToken ct)
    {
        var shortCode = !string.IsNullOrWhiteSpace(request.CustomAlias)
            ? await ValidateAlias(request.CustomAlias, ct)
            : await GenerateUnique(ct);

        DateTimeOffset? expiry = null;
        if (!string.IsNullOrWhiteSpace(request.ExpiresAt))
            expiry = DateTimeOffset.Parse(request.ExpiresAt);

        var url = ShortUrl.Create(shortCode, request.LongUrl, request.CustomAlias, expiry);
        var saved = await _repo.AddAsync(url, ct);

        await _cache.SetAsync($"url:{shortCode}", saved.LongUrl,
            expiry.HasValue ? expiry.Value - DateTimeOffset.UtcNow : TimeSpan.FromHours(24), ct);

        await _publisher.PublishAsync("url-events", new UrlCreatedEvent(
            saved.Id, saved.ShortCode, saved.LongUrl, saved.CustomAlias, saved.ExpiresAt, DateTimeOffset.UtcNow), ct);

        return ToDto(saved);
    }

    private async Task<string> ValidateAlias(string alias, CancellationToken ct)
    {
        if (await _repo.ShortCodeExistsAsync(alias, ct))
            throw new InvalidOperationException($"Alias '{alias}' is already taken.");
        return alias;
    }

    private async Task<string> GenerateUnique(CancellationToken ct)
    {
        for (int i = 0; i < 5; i++)
        {
            var code = _gen.Generate();
            if (!await _repo.ShortCodeExistsAsync(code, ct)) return code;
        }
        throw new InvalidOperationException("Failed to generate unique code.");
    }

    private static ShortUrlDto ToDto(ShortUrl u) => new(u.Id, u.ShortCode, u.LongUrl, u.CustomAlias, u.ClickCount,
        u.ExpiresAt?.ToString("O"), u.CreatedAt.ToString("O"), u.UpdatedAt.ToString("O"));
}
