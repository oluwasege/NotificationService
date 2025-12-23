using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Exceptions;
using NotificationService.Infrastructure.Extensions;

namespace NotificationService.Infrastructure.Data;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(500).IsRequired();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SubscriptionKey).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasIndex(e => e.SubscriptionKey).IsUnique();
            entity.HasOne(e => e.User)
                .WithMany(u => u.Subscriptions)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Recipient).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Subject).HasMaxLength(500);
            entity.Property(e => e.Body).IsRequired();
            entity.Property(e => e.Metadata).HasMaxLength(4000);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            entity.Property(e => e.ExternalId).HasMaxLength(256);
            entity.Property(e => e.CorrelationId).HasMaxLength(64);
            entity.Property(e => e.IdempotencyKey).HasMaxLength(64);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ScheduledAt);
            entity.HasIndex(e => e.CorrelationId);
            entity.HasIndex(e => e.IdempotencyKey)
                .IsUnique()
                .HasFilter("[IdempotencyKey] IS NOT NULL");
            entity.HasOne(e => e.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Subscription)
                .WithMany(s => s.Notifications)
                .HasForeignKey(e => e.SubscriptionId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Template)
                .WithMany(t => t.Notifications)
                .HasForeignKey(e => e.TemplateId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<NotificationLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Message).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.Details).HasMaxLength(4000);
            entity.Property(e => e.ProviderResponse).HasMaxLength(4000);
            entity.HasOne(e => e.Notification)
                .WithMany(n => n.Logs)
                .HasForeignKey(e => e.NotificationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MessageType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Payload).IsRequired();
            entity.Property(e => e.Error).HasMaxLength(2000);
            entity.HasIndex(e => new { e.ProcessedAt, e.CreatedAt })
                .HasDatabaseName("IX_OutboxMessages_Unprocessed");
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<NotificationTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.SubjectTemplate).HasMaxLength(500);
            entity.Property(e => e.BodyTemplate).IsRequired();
            entity.HasIndex(e => new { e.SubscriptionId, e.Name })
                .IsUnique()
                .HasDatabaseName("IX_NotificationTemplates_Subscription_Name");
            entity.HasOne(e => e.Subscription)
                .WithMany(s => s.Templates)
                .HasForeignKey(e => e.SubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<WebhookSubscription>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Url).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.Secret).HasMaxLength(256);
            entity.Property(e => e.Events).HasMaxLength(500);
            entity.HasOne(e => e.Subscription)
                .WithMany(s => s.Webhooks)
                .HasForeignKey(e => e.SubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // Apply soft delete query filter to all entities
        var entityTypes = modelBuilder.Model.GetEntityTypes();
        foreach (var entityType in entityTypes)
        {
            modelBuilder.AddSoftDeleteQueryFilter(entityType);
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            ApplyAuditInfo();
            var result = await base.SaveChangesAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new AppDbConcurrencyException(ex.Message, ex);
        }
    }

    private void ApplyAuditInfo()
    {
        var time = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            // Handle entities that inherit from BaseEntity<Guid>
            if (entry.Entity is BaseEntity<Guid> guidEntity)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        guidEntity.IsDeleted = false;
                        guidEntity.CreatedAt = time;
                        break;
                    case EntityState.Deleted:
                        entry.State = EntityState.Modified;
                        guidEntity.IsDeleted = true;
                        guidEntity.UpdatedAt = time;
                        break;
                    case EntityState.Modified:
                        guidEntity.UpdatedAt = time;
                        break;
                }
            }
        }
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ConfigureWarnings(x => x.Ignore(SqlServerEventId.SavepointsDisabledBecauseOfMARS));
    }
}
