using Microsoft.EntityFrameworkCore;
using TinyUrl.UrlService.Application.Interfaces;
using TinyUrl.UrlService.Domain.Entities;
using TinyUrl.UrlService.Infrastructure.Data;

namespace TinyUrl.UrlService.Infrastructure.Repositories;

public class UrlRepository(UrlServiceDbContext db) : IUrlRepository
{
    public Task<ShortUrl?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Urls.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<ShortUrl?> GetByShortCodeAsync(string code, CancellationToken ct = default) =>
        db.Urls.FirstOrDefaultAsync(u => u.ShortCode == code, ct);

    public async Task<(IEnumerable<ShortUrl> Urls, int Total)> ListAsync(int page, int limit, string? search, CancellationToken ct = default)
    {
        var q = db.Urls.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(u => u.LongUrl.Contains(search) || u.ShortCode.Contains(search));
        var total = await q.CountAsync(ct);
        var urls = await q.OrderByDescending(u => u.CreatedAt).Skip((page - 1) * limit).Take(limit).ToListAsync(ct);
        return (urls, total);
    }

    public async Task<ShortUrl> AddAsync(ShortUrl url, CancellationToken ct = default)
    { db.Urls.Add(url); await db.SaveChangesAsync(ct); return url; }

    public async Task UpdateAsync(ShortUrl url, CancellationToken ct = default)
    { db.Urls.Update(url); await db.SaveChangesAsync(ct); }

    public Task<bool> ShortCodeExistsAsync(string code, CancellationToken ct = default) =>
        db.Urls.IgnoreQueryFilters().AnyAsync(u => u.ShortCode == code, ct);
}
