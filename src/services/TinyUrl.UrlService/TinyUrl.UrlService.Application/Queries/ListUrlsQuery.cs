using MediatR;
using TinyUrl.UrlService.Application.DTOs;
using TinyUrl.UrlService.Application.Interfaces;

namespace TinyUrl.UrlService.Application.Queries;

public record ListUrlsQuery(int Page = 1, int Limit = 20, string? Search = null) : IRequest<UrlListResponseDto>;

public class ListUrlsHandler : IRequestHandler<ListUrlsQuery, UrlListResponseDto>
{
    private readonly IUrlRepository _repo;
    public ListUrlsHandler(IUrlRepository repo) => _repo = repo;

    public async Task<UrlListResponseDto> Handle(ListUrlsQuery request, CancellationToken ct)
    {
        var (urls, total) = await _repo.ListAsync(request.Page, request.Limit, request.Search, ct);
        var dtos = urls.Select(u => new ShortUrlDto(u.Id, u.ShortCode, u.LongUrl, u.CustomAlias, u.ClickCount,
            u.ExpiresAt?.ToString("O"), u.CreatedAt.ToString("O"), u.UpdatedAt.ToString("O")));
        return new(dtos, total, request.Page, request.Limit);
    }
}

public record GetUrlQuery(Guid Id) : IRequest<ShortUrlDto>;

public class GetUrlHandler : IRequestHandler<GetUrlQuery, ShortUrlDto>
{
    private readonly IUrlRepository _repo;
    public GetUrlHandler(IUrlRepository repo) => _repo = repo;

    public async Task<ShortUrlDto> Handle(GetUrlQuery request, CancellationToken ct)
    {
        var u = await _repo.GetByIdAsync(request.Id, ct) ?? throw new KeyNotFoundException($"URL '{request.Id}' not found.");
        return new(u.Id, u.ShortCode, u.LongUrl, u.CustomAlias, u.ClickCount,
            u.ExpiresAt?.ToString("O"), u.CreatedAt.ToString("O"), u.UpdatedAt.ToString("O"));
    }
}
