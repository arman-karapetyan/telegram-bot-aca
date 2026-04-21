using Microsoft.EntityFrameworkCore;
using telegram_bot_aca.Data.Entites;

namespace telegram_bot_aca.Data;

public class AppDbContext:DbContext
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<ProcessingJob> Jobs => Set<ProcessingJob>();
    public DbSet<QueueStateEntry> QueueStates => Set<QueueStateEntry>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Username).HasMaxLength(64);
        });

        modelBuilder.Entity<ProcessingJob>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.User)
                .WithMany(x => x.Jobs)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<QueueStateEntry>(entity =>
        {
            entity.HasKey(x => x.Id);
        });
    }
}