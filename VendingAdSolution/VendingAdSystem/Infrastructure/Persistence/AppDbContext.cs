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
    public DbSet<Playlist> Playlists => Set<Playlist>();
    public DbSet<PlaylistItem> PlaylistItems => Set<PlaylistItem>();
    public DbSet<PlaybackSchedule> PlaybackSchedules => Set<PlaybackSchedule>();
    public DbSet<PlaybackScheduleDevice> PlaybackScheduleDevices => Set<PlaybackScheduleDevice>();
    public DbSet<PlaybackScheduleItem> PlaybackScheduleItems => Set<PlaybackScheduleItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Device>()
            .HasIndex(d => d.DeviceCode)
            .IsUnique();

        modelBuilder.Entity<Device>()
            .HasIndex(d => d.UserId);

        modelBuilder.Entity<Device>()
            .HasIndex(d => d.IsActive);

        modelBuilder.Entity<Device>()
            .HasIndex(d => d.ClaimCode);

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
            .HasIndex(m => m.UserId);

        modelBuilder.Entity<Media>()
            .HasIndex(m => m.UploadedAt);

        modelBuilder.Entity<Media>()
            .HasOne(m => m.User)
            .WithMany(u => u.Medias)
            .HasForeignKey(m => m.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Playlist>()
            .HasOne(p => p.User)
            .WithMany(u => u.Playlists)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PlaylistItem>()
            .HasOne(pi => pi.Playlist)
            .WithMany(p => p.Items)
            .HasForeignKey(pi => pi.PlaylistId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PlaylistItem>()
            .HasOne(pi => pi.Media)
            .WithMany(m => m.PlaylistItems)
            .HasForeignKey(pi => pi.MediaId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PlaybackSchedule>()
            .HasIndex(ps => ps.UserId);

        modelBuilder.Entity<PlaybackSchedule>()
            .HasIndex(ps => ps.IsActive);

        modelBuilder.Entity<PlaybackSchedule>()
            .HasIndex(ps => ps.StartDate);

        modelBuilder.Entity<PlaybackSchedule>()
            .HasIndex(ps => ps.EndDate);

        modelBuilder.Entity<PlaybackSchedule>()
            .HasIndex(ps => ps.CreatedAt);

        modelBuilder.Entity<PlaybackSchedule>()
            .HasIndex(ps => new { ps.IsActive, ps.StartDate, ps.EndDate });

        modelBuilder.Entity<PlaybackSchedule>()
            .HasIndex(ps => new { ps.UserId, ps.IsActive, ps.CreatedAt });

        modelBuilder.Entity<PlaybackSchedule>()
            .HasOne(ps => ps.User)
            .WithMany(u => u.PlaybackSchedules)
            .HasForeignKey(ps => ps.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PlaybackScheduleDevice>()
            .HasIndex(psd => psd.DeviceId);

        modelBuilder.Entity<PlaybackScheduleDevice>()
            .HasIndex(psd => psd.PlaybackScheduleId);

        modelBuilder.Entity<PlaybackScheduleDevice>()
            .HasIndex(psd => new { psd.DeviceId, psd.PlaybackScheduleId });

        modelBuilder.Entity<PlaybackScheduleDevice>()
            .HasOne(psd => psd.PlaybackSchedule)
            .WithMany(ps => ps.Devices)
            .HasForeignKey(psd => psd.PlaybackScheduleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PlaybackScheduleDevice>()
            .HasOne(psd => psd.Device)
            .WithMany(d => d.PlaybackScheduleDevices)
            .HasForeignKey(psd => psd.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PlaybackScheduleItem>()
            .HasIndex(psi => psi.PlaybackScheduleId);

        modelBuilder.Entity<PlaybackScheduleItem>()
            .HasIndex(psi => new { psi.PlaybackScheduleId, psi.OrderIndex });

        modelBuilder.Entity<PlaybackScheduleItem>()
            .HasOne(psi => psi.PlaybackSchedule)
            .WithMany(ps => ps.Items)
            .HasForeignKey(psi => psi.PlaybackScheduleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PlaybackScheduleItem>()
            .HasOne(psi => psi.Media)
            .WithMany(m => m.PlaybackScheduleItems)
            .HasForeignKey(psi => psi.MediaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
