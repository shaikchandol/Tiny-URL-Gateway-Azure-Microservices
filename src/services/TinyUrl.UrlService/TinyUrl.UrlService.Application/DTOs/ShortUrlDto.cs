namespace TinyUrl.UrlService.Application.DTOs;

public record ShortUrlDto(Guid Id, string ShortCode, string LongUrl, string? CustomAlias, int ClickCount, string? ExpiresAt, string CreatedAt, string UpdatedAt);
public record UrlListResponseDto(IEnumerable<ShortUrlDto> Urls, int Total, int Page, int Limit);
