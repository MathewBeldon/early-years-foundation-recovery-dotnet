using EarlyYearsFoundationRecovery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EarlyYearsFoundationRecovery.Infrastructure.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserModuleProgress> UserModuleProgress => Set<UserModuleProgress>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<Response> Responses => Set<Response>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<MailEvent> MailEvents => Set<MailEvent>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<Event> Events => Set<Event>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasIndex(u => u.GovOneId).IsUnique();
            entity.Property(u => u.Email).IsRequired();
        });

        modelBuilder.Entity<UserModuleProgress>(entity =>
        {
            entity.ToTable("user_module_progress");
            entity.HasIndex(p => new { p.UserId, p.ModuleName }).IsUnique();
            entity.Property(p => p.VisitedPages).AsJsonbDictionary();
            entity.HasOne(p => p.User).WithMany(u => u.ModuleProgress).HasForeignKey(p => p.UserId);
        });

        modelBuilder.Entity<Assessment>(entity =>
        {
            entity.ToTable("assessments");
            entity.HasIndex(a => new { a.UserId, a.TrainingModule, a.StartedAt })
                .IsDescending(false, false, true);
            entity.HasIndex(a => new { a.UserId, a.TrainingModule })
                .IsUnique()
                .HasFilter("completed_at IS NULL");
            entity.HasOne(a => a.User).WithMany(u => u.Assessments).HasForeignKey(a => a.UserId);
        });

        modelBuilder.Entity<Response>(entity =>
        {
            entity.ToTable("responses");
            entity.HasIndex(r => new { r.UserId, r.TrainingModule, r.QuestionName });
            entity.HasIndex(r => new { r.UserId, r.TrainingModule, r.AssessmentId, r.QuestionName });
            entity.Property(r => r.Answers).AsJsonbList();
            entity.HasOne(r => r.User).WithMany(u => u.Responses).HasForeignKey(r => r.UserId);
            entity.HasOne(r => r.Assessment).WithMany(a => a.Responses).HasForeignKey(r => r.AssessmentId);
        });

        modelBuilder.Entity<Note>(entity =>
        {
            entity.ToTable("notes");
            entity.HasIndex(n => new { n.UserId, n.TrainingModule, n.UpdatedAt })
                .IsDescending(false, false, true);
            entity.HasIndex(n => new { n.UserId, n.TrainingModule, n.Name });
            entity.HasOne(n => n.User).WithMany(u => u.Notes).HasForeignKey(n => n.UserId);
        });

        modelBuilder.Entity<MailEvent>(entity =>
        {
            entity.ToTable("mail_events");
            entity.Property(m => m.Personalisation).AsJsonbDictionary();
            entity.Property(m => m.Callback)
                .HasConversion(
                    value => value == null ? null : JsonSerializer.Serialize(value, JsonPropertyExtensions.JsonOptions),
                    value => value == null ? null : JsonSerializer.Deserialize<Dictionary<string, object?>>(value, JsonPropertyExtensions.JsonOptions))
                .HasColumnType("jsonb");
            entity.Property(m => m.Callback).Metadata
                .SetValueComparer(JsonPropertyExtensions.CreateJsonValueComparer<Dictionary<string, object?>>());
            entity.HasOne(m => m.User).WithMany(u => u.MailEvents).HasForeignKey(m => m.UserId);
        });

        modelBuilder.Entity<Visit>(entity =>
        {
            entity.ToTable("visits");
            entity.HasOne(v => v.User).WithMany().HasForeignKey(v => v.UserId);
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.ToTable("events");
            entity.Property(e => e.Properties).AsJsonbDictionary();
            entity.HasOne(e => e.Visit).WithMany().HasForeignKey(e => e.VisitId);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyTimestamps()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<ITimestamped>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
    }
}
