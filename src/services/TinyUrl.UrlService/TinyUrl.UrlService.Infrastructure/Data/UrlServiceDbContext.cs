using Microsoft.EntityFrameworkCore;
using TinyUrl.UrlService.Domain.Entities;

namespace TinyUrl.UrlService.Infrastructure.Data;

public class UrlServiceDbContext(DbContextOptions<UrlServiceDbContext> options) : DbContext(options)
{
    public DbSet<ShortUrl> Urls => Set<ShortUrl>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<ShortUrl>(e =>
        {
            e.ToTable("urls");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ShortCode).HasColumnName("short_code").IsRequired().HasMaxLength(50);
            e.Property(x => x.LongUrl).HasColumnName("long_url").IsRequired();
            e.Property(x => x.CustomAlias).HasColumnName("custom_alias").HasMaxLength(50);
            e.Property(x => x.ClickCount).HasColumnName("click_count").HasDefaultValue(0);
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => x.ShortCode).IsUnique();
            e.HasQueryFilter(x => !x.IsDeleted);
        });
    }
}
