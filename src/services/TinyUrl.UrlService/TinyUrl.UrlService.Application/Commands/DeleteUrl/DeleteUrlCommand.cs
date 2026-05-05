using MediatR;

namespace TinyUrl.UrlService.Application.Commands.DeleteUrl;

public record DeleteUrlCommand(Guid Id) : IRequest;
