using MediatR;
using TinyUrl.UrlService.Application.DTOs;

namespace TinyUrl.UrlService.Application.Commands.UpdateUrl;

public record UpdateUrlCommand(Guid Id, string? LongUrl, string? ExpiresAt) : IRequest<ShortUrlDto>;
