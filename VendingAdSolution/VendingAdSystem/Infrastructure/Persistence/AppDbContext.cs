using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Domain.Entities;

namespace VendingAdSystem.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Media> Medias => Set<Media>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Device>()
            .HasOne(d => d.User)
            .WithMany(u => u.Devices)
            .HasForeignKey(d => d.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<Media>()
            .HasOne(m => m.User)
            .WithMany(u => u.Medias)
            .HasForeignKey(m => m.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Campaign>()
            .HasOne(c => c.Device)
            .WithMany(d => d.Campaigns)
            .HasForeignKey(c => c.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Campaign>()
            .HasOne(c => c.Media)
            .WithMany(m => m.Campaigns)
            .HasForeignKey(c => c.MediaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
