using Microsoft.EntityFrameworkCore;

namespace Durnius.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<User>(entity =>
    {
        entity.Property(user => user.Username)
            .HasMaxLength(32)
            .IsRequired();

        entity.Property(user => user.PasswordHash)
            .HasMaxLength(256)
            .IsRequired();

        entity.HasIndex(user => user.Username)
            .IsUnique();
    });
}
}