using MediatR;
using TinyUrl.UrlService.Application.DTOs;

namespace TinyUrl.UrlService.Application.Commands.CreateUrl;

public record CreateUrlCommand(string LongUrl, string? CustomAlias, string? ExpiresAt) : IRequest<ShortUrlDto>;
