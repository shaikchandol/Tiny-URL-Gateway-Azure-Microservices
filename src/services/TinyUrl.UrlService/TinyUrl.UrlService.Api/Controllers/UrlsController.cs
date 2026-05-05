using MediatR;
using Microsoft.AspNetCore.Mvc;
using TinyUrl.UrlService.Application.Commands.CreateUrl;
using TinyUrl.UrlService.Application.Commands.DeleteUrl;
using TinyUrl.UrlService.Application.Commands.UpdateUrl;
using TinyUrl.UrlService.Application.Queries;

namespace TinyUrl.UrlService.Api.Controllers;

[ApiController]
[Route("api/urls")]
[Produces("application/json")]
public class UrlsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int limit = 20, [FromQuery] string? search = null, CancellationToken ct = default)
        => Ok(await mediator.Send(new ListUrlsQuery(page, limit, search), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetUrlQuery(id), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUrlCommand cmd, CancellationToken ct = default)
    {
        var result = await mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUrlRequest body, CancellationToken ct = default)
        => Ok(await mediator.Send(new UpdateUrlCommand(id, body.LongUrl, body.ExpiresAt), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await mediator.Send(new DeleteUrlCommand(id), ct);
        return NoContent();
    }
}

public record UpdateUrlRequest(string? LongUrl, string? ExpiresAt);
