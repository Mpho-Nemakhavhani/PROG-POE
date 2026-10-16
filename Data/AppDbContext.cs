using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>
/// EF Core context. The model configuration mirrors the Part 1 SQL script
/// (table names, column sizes, unique constraints, check constraints and delete behaviours).
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<RouteInfo> RouteInfos => Set<RouteInfo>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Result> Results => Set<Result>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<User>(e =>
        {
            e.ToTable("Users", t => t.HasCheckConstraint("CK_Users_Role", "[Role] IN ('Organiser', 'Participant')"));
            e.HasKey(x => x.UserId);
            e.Property(x => x.FullName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Email).HasMaxLength(150).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(256).IsRequired();
            e.Property(x => x.Role).HasMaxLength(20).IsRequired();
            e.Property(x => x.PhoneNumber).HasMaxLength(20);
            e.Property(x => x.CreatedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSDATETIME()");
        });

        mb.Entity<Event>(e =>
        {
            e.ToTable("Events", t =>
            {
                t.HasCheckConstraint("CK_Events_Type", "[EventType] IN ('Run', 'Walk', 'Cycle')");
                t.HasCheckConstraint("CK_Events_Status", "[Status] IN ('Upcoming', 'Completed', 'Cancelled')");
            });
            e.HasKey(x => x.EventId);
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.EventType).HasMaxLength(20).IsRequired();
            e.Property(x => x.EventDate).HasColumnType("datetime2");
            e.Property(x => x.Location).HasMaxLength(150).IsRequired();
            e.Property(x => x.Province).HasMaxLength(50).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSDATETIME()");
            e.HasOne(x => x.Organiser).WithMany(u => u.OrganisedEvents)
                .HasForeignKey(x => x.OrganiserId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Category>(e =>
        {
            e.ToTable("Categories");
            e.HasKey(x => x.CategoryId);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.DistanceKm).HasPrecision(6, 2);
            e.Property(x => x.EntryFee).HasPrecision(8, 2);
            e.HasOne(x => x.Event).WithMany(ev => ev.Categories)
                .HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<RouteInfo>(e =>
        {
            e.ToTable("RouteInfo");
            e.HasKey(x => x.RouteId);
            e.Property(x => x.RouteMapUrl).HasMaxLength(300);
            e.Property(x => x.StartLatitude).HasPrecision(9, 6);
            e.Property(x => x.StartLongitude).HasPrecision(9, 6);
            e.Property(x => x.WeatherNotes).HasMaxLength(500);
            e.HasIndex(x => x.EventId).IsUnique();
            e.HasOne(x => x.Event).WithOne(ev => ev.Route)
                .HasForeignKey<RouteInfo>(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Enrolment>(e =>
        {
            e.ToTable("Enrolments", t => t.HasCheckConstraint("CK_Enrolments_Status", "[Status] IN ('Pending', 'Confirmed', 'Cancelled')"));
            e.HasKey(x => x.EnrolmentId);
            e.Property(x => x.EnrolmentDate).HasColumnType("datetime2").HasDefaultValueSql("SYSDATETIME()");
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.RaceNumber).HasMaxLength(20);
            e.HasIndex(x => new { x.ParticipantId, x.CategoryId }).IsUnique();
            e.HasOne(x => x.Participant).WithMany(u => u.Enrolments)
                .HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category).WithMany(c => c.Enrolments)
                .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Result>(e =>
        {
            e.ToTable("Results", t => t.HasCheckConstraint("CK_Results_Status", "[Status] IN ('Finished', 'DNF', 'DSQ')"));
            e.HasKey(x => x.ResultId);
            e.Property(x => x.FinishTime).HasColumnType("time");
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.CapturedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSDATETIME()");
            e.HasIndex(x => x.EnrolmentId).IsUnique();
            e.HasOne(x => x.Enrolment).WithOne(en => en.Result)
                .HasForeignKey<Result>(x => x.EnrolmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.CapturedByOrganiser).WithMany()
                .HasForeignKey(x => x.CapturedByOrganiserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
