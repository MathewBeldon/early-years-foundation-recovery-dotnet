using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Notes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace EarlyYearsFoundationRecovery.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    private readonly INoteBodyProtector _noteBodyProtector;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        INoteBodyProtector noteBodyProtector)
        : base(options)
    {
        _noteBodyProtector = noteBodyProtector ?? throw new ArgumentNullException(nameof(noteBodyProtector));
    }

    internal INoteBodyProtector NoteBodyProtector => _noteBodyProtector;

    public DbSet<User> Users => Set<User>();
    public DbSet<UserModuleProgress> UserModuleProgress => Set<UserModuleProgress>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<Response> Responses => Set<Response>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<MailEvent> MailEvents => Set<MailEvent>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<ConfidenceCheckProgress> ConfidenceCheckProgress => Set<ConfidenceCheckProgress>();
    public DbSet<Release> Releases => Set<Release>();
    public DbSet<ModuleRelease> ModuleReleases => Set<ModuleRelease>();
    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasIndex(u => u.GovOneId).IsUnique();
            entity.Property(u => u.Email).IsRequired();
            // Rails v1.5.0 users.private_beta_registration_complete. Keep the
            // shared Rails-owned column explicit; no migration owns it here.
            entity.Property(u => u.PrivateBetaRegistrationComplete)
                .HasColumnName("private_beta_registration_complete");
            // Rails v1.5.0 ac546721 app/models/trainee/setting.rb and
            // app/forms/registration/setting_type_other_form.rb persist the
            // canonical ID and reporting-title snapshot as separate values.
            entity.Property(u => u.SettingTypeId).HasColumnName("setting_type_id");
            entity.Property(u => u.SettingType).HasColumnName("setting_type");
            entity.Property(u => u.NotifyCallback)
                .HasConversion(
                    value => value == null ? null : JsonSerializer.Serialize(value, JsonPropertyExtensions.JsonOptions),
                    value => value == null ? null : JsonSerializer.Deserialize<Dictionary<string, object?>>(value, JsonPropertyExtensions.JsonOptions))
                .HasColumnType("jsonb");
            entity.Property(u => u.NotifyCallback).Metadata
                .SetValueComparer(JsonPropertyExtensions.CreateJsonValueComparer<Dictionary<string, object?>?>());
        });

        modelBuilder.Entity<UserModuleProgress>(entity =>
        {
            entity.ToTable("user_module_progress");
            entity.HasIndex(p => new { p.UserId, p.ModuleName }).IsUnique();
            // Rails v1.5.0 stores ISO8601 timestamps; existing .NET rows may
            // still contain booleans. Membership is page-key presence.
            entity.Property(p => p.VisitedPages).AsVisitedPagesJsonb();
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
            entity.Property(n => n.Body)
                .HasConversion(new ValueConverter<string?, string?>(
                    plaintext => plaintext == null ? null : _noteBodyProtector.Protect(plaintext),
                    ciphertext => ciphertext == null ? null : _noteBodyProtector.Unprotect(ciphertext)))
                .HasColumnType("text");
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

        modelBuilder.Entity<ConfidenceCheckProgress>(entity =>
        {
            entity.ToTable("confidence_check_progress");
            entity.HasIndex(x => new { x.UserId, x.ModuleName, x.CheckType }).IsUnique();
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<Release>(entity =>
        {
            entity.ToTable("releases");
            entity.Property(x => x.Properties).AsJsonbDictionary();
            entity.HasMany(x => x.Modules).WithOne(x => x.Release).HasForeignKey(x => x.ReleaseId);
        });

        modelBuilder.Entity<ModuleRelease>(entity =>
        {
            entity.ToTable("module_releases");
            entity.HasIndex(x => x.Name).IsUnique();
            entity.HasIndex(x => x.ModulePosition).IsUnique();
        });

        modelBuilder.Entity<BackgroundJob>(entity =>
        {
            entity.ToTable("background_jobs");
            entity.Property(x => x.Payload).HasColumnType("jsonb");
            entity.HasIndex(x => new { x.Status, x.RunAt });
        });
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.ReplaceService<Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory, NoteEncryptionModelCacheKeyFactory>();

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

    internal Task<int> SaveChangesWithoutApplyingTimestampsAsync(CancellationToken cancellationToken = default) =>
        base.SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);

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
