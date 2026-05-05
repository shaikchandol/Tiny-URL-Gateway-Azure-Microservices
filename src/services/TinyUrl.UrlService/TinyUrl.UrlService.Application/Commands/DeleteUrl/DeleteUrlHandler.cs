using MediatR;
using TinyUrl.Contracts.Events;
using TinyUrl.UrlService.Application.Interfaces;

namespace TinyUrl.UrlService.Application.Commands.DeleteUrl;

public class DeleteUrlHandler : IRequestHandler<DeleteUrlCommand>
{
    private readonly IUrlRepository _repo;
    private readonly IEventPublisher _publisher;
    private readonly ICacheService _cache;

    public DeleteUrlHandler(IUrlRepository repo, IEventPublisher publisher, ICacheService cache)
    { _repo = repo; _publisher = publisher; _cache = cache; }

    public async Task Handle(DeleteUrlCommand request, CancellationToken ct)
    {
        var url = await _repo.GetByIdAsync(request.Id, ct) ?? throw new KeyNotFoundException($"URL '{request.Id}' not found.");
        url.SoftDelete();
        await _repo.UpdateAsync(url, ct);
        await _cache.RemoveAsync($"url:{url.ShortCode}", ct);
        await _publisher.PublishAsync("url-events", new UrlDeletedEvent(url.Id, url.ShortCode, DateTimeOffset.UtcNow), ct);
    }
}
