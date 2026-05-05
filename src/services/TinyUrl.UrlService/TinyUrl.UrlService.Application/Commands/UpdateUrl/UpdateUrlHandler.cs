using MediatR;
using TinyUrl.UrlService.Application.DTOs;
using TinyUrl.UrlService.Application.Interfaces;

namespace TinyUrl.UrlService.Application.Commands.UpdateUrl;

public class UpdateUrlHandler : IRequestHandler<UpdateUrlCommand, ShortUrlDto>
{
    private readonly IUrlRepository _repo;
    private readonly ICacheService _cache;

    public UpdateUrlHandler(IUrlRepository repo, ICacheService cache) { _repo = repo; _cache = cache; }

    public async Task<ShortUrlDto> Handle(UpdateUrlCommand request, CancellationToken ct)
    {
        var url = await _repo.GetByIdAsync(request.Id, ct) ?? throw new KeyNotFoundException($"URL '{request.Id}' not found.");
        if (!string.IsNullOrWhiteSpace(request.LongUrl)) url.UpdateLongUrl(request.LongUrl);
        if (request.ExpiresAt is not null) url.UpdateExpiry(string.IsNullOrWhiteSpace(request.ExpiresAt) ? null : DateTimeOffset.Parse(request.ExpiresAt));
        await _repo.UpdateAsync(url, ct);
        await _cache.RemoveAsync($"url:{url.ShortCode}", ct);
        return new(url.Id, url.ShortCode, url.LongUrl, url.CustomAlias, url.ClickCount,
            url.ExpiresAt?.ToString("O"), url.CreatedAt.ToString("O"), url.UpdatedAt.ToString("O"));
    }
}
