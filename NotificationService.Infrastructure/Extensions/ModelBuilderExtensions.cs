using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NotificationService.Domain.Interfaces;
using System.Linq.Expressions;

namespace NotificationService.Infrastructure.Extensions
{
    public static class ModelBuilderExtensions
    {
        public static void AddSoftDeleteQueryFilter(this ModelBuilder builder, IMutableEntityType entityType)
        {
            if (typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var property = Expression.Property(parameter, nameof(ISoftDelete.IsDeleted));
                var condition = Expression.MakeBinary(ExpressionType.Equal, property, Expression.Constant(false));
                var lambda = Expression.Lambda(condition, parameter);

                builder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
    }
}
