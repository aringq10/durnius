using Microsoft.EntityFrameworkCore;

namespace Durnius.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<MatchHistory> MatchHistories => Set<MatchHistory>();
    public DbSet<MatchParticipant> MatchParticipants => Set<MatchParticipant>();

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

        modelBuilder.Entity<MatchParticipant>(entity =>
        {
            entity.HasKey(mp => new { mp.MatchHistoryId, mp.UserId });

            entity.HasOne(mp => mp.MatchHistory)
                .WithMany(mh => mh.Participants)
                .HasForeignKey(mp => mp.MatchHistoryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(mp => mp.User)
                .WithMany(u => u.MatchParticipations)
                .HasForeignKey(mp => mp.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(mp => mp.UserId);
        });

        modelBuilder.Entity<MatchHistory>(entity =>
        {
            entity.Property(mh => mh.GameType).HasMaxLength(32);
            entity.Property(mh => mh.MatchDate).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }
}
