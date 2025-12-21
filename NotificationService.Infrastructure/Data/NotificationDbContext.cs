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
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ScheduledAt);
            entity.HasIndex(e => e.CorrelationId);
            entity.HasOne(e => e.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Subscription)
                .WithMany(s => s.Notifications)
                .HasForeignKey(e => e.SubscriptionId)
                .OnDelete(DeleteBehavior.NoAction);
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

        // Get all entity types
        var entityTypes = modelBuilder.Model.GetEntityTypes();

        foreach (var entityType in entityTypes)
        {

            // Check if the entity type is a generic base entity
            if (entityType.ClrType.IsGenericType && entityType.ClrType.GetGenericTypeDefinition() == typeof(BaseEntity<>))
            {
                // Get the type of T (Id type)
                var idType = entityType.ClrType.GetGenericArguments()[0];

                // Configure primary key as non-clustered
                modelBuilder.Entity(entityType.ClrType)
                    .HasKey("Id");

                // Configure Id property based on its type
                if (idType == typeof(int) || idType == typeof(long))
                {
                    modelBuilder.Entity(entityType.ClrType)
                        .Property("Id")
                        .UseIdentityColumn();
                }
                else if (idType == typeof(Guid))
                {
                    modelBuilder.Entity(entityType.ClrType)
                        .Property("Id")
                        .HasDefaultValueSql("NEWSEQUENTIALID()");
                }

                // Configure clustered index on DateCreated
                modelBuilder.Entity(entityType.ClrType)
                    .HasIndex("DateCreated")
                    .HasDatabaseName($"CIX_{entityType.GetTableName()}_DateCreated")
                    .IsClustered();

                // Configure DateCreated property
                modelBuilder.Entity(entityType.ClrType)
                    .Property("DateCreated")
                    .HasDefaultValueSql("SYSDATETIMEOFFSET()")
                    .ValueGeneratedOnAdd();
            }
            modelBuilder.AddSoftDeleteQueryFilter(entityType);
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {

        try
        {
            CrudStatuses();
            var result = await base.SaveChangesAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new AppDbConcurrencyException(ex.Message, ex);
        }
    }

    private void CrudStatuses()
    {
        var time = DateTime.Now;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is BaseEntity<object> baseEntity)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        baseEntity.IsDeleted = false;
                        baseEntity.CreatedAt = time;
                        break;
                    case EntityState.Deleted:
                        entry.State = EntityState.Modified;
                        baseEntity.IsDeleted = true;
                        baseEntity.UpdatedAt = time;
                        break;
                    case EntityState.Modified:
                        baseEntity.UpdatedAt = time;
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
