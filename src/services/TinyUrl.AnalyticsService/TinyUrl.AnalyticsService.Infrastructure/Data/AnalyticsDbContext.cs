using Microsoft.EntityFrameworkCore;

namespace TinyUrl.AnalyticsService.Infrastructure.Data;

public class ClickRecord
{
    public Guid Id { get; set; }
    public Guid UrlId { get; set; }
    public string ShortCode { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    public DateTimeOffset ClickedAt { get; set; }
}

public class AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options) : DbContext(options)
{
    public DbSet<ClickRecord> ClickRecords => Set<ClickRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<ClickRecord>(e =>
        {
            e.ToTable("click_records");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UrlId).HasColumnName("url_id");
            e.Property(x => x.ShortCode).HasColumnName("short_code").IsRequired().HasMaxLength(50);
            e.Property(x => x.UserAgent).HasColumnName("user_agent");
            e.Property(x => x.IpAddress).HasColumnName("ip_address");
            e.Property(x => x.ClickedAt).HasColumnName("clicked_at");
            e.HasIndex(x => x.ShortCode);
            e.HasIndex(x => x.ClickedAt);
        });
    }
}
