using Microsoft.EntityFrameworkCore;

namespace ElenskiBalkandzii.Server.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<SiteContent> SiteContents => Set<SiteContent>();
    public DbSet<SiteImage> SiteImages => Set<SiteImage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<SiteContent>(entity =>
        {
            entity.ToTable("site_content");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Json).HasColumnName("json").HasColumnType("jsonb");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });
        modelBuilder.Entity<SiteImage>(entity =>
        {
            entity.ToTable("site_images");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ContentType).HasColumnName("content_type");
            entity.Property(x => x.Data).HasColumnName("data");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        });
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
