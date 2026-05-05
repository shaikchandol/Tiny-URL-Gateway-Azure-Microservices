using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TinyUrl.AnalyticsService.Infrastructure.Data;

namespace TinyUrl.AnalyticsService.Api.Controllers;

[ApiController]
[Route("api/analytics")]
[Produces("application/json")]
public class AnalyticsController(AnalyticsDbContext db) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var totalClicks = await db.ClickRecords.CountAsync(ct);
        var today = DateTimeOffset.UtcNow.Date;
        var clicksToday = await db.ClickRecords.CountAsync(r => r.ClickedAt >= today, ct);
        var topCodes = await db.ClickRecords
            .GroupBy(r => r.ShortCode)
            .Select(g => new { ShortCode = g.Key, Clicks = g.Count() })
            .OrderByDescending(g => g.Clicks)
            .Take(5)
            .ToListAsync(ct);

        return Ok(new { totalClicks, clicksToday, topCodes });
    }

    [HttpGet("clicks/{shortCode}")]
    public async Task<IActionResult> ClicksByCode(string shortCode, [FromQuery] int days = 30, CancellationToken ct = default)
    {
        var from = DateTimeOffset.UtcNow.AddDays(-days);
        var data = await db.ClickRecords
            .Where(r => r.ShortCode == shortCode && r.ClickedAt >= from)
            .GroupBy(r => r.ClickedAt.Date)
            .Select(g => new { Date = g.Key.ToString("yyyy-MM-dd"), Clicks = g.Count() })
            .OrderBy(g => g.Date)
            .ToListAsync(ct);

        return Ok(new { shortCode, totalClicks = data.Sum(d => d.Clicks), data });
    }

    [HttpGet("top")]
    public async Task<IActionResult> TopUrls([FromQuery] int limit = 10, CancellationToken ct = default)
    {
        var top = await db.ClickRecords
            .GroupBy(r => r.ShortCode)
            .Select(g => new { ShortCode = g.Key, Clicks = g.Count() })
            .OrderByDescending(g => g.Clicks)
            .Take(limit)
            .ToListAsync(ct);

        return Ok(top);
    }
}
