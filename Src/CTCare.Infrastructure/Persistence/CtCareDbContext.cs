using System.Linq.Expressions;
using System.Reflection.Emit;

using CTCare.Domain.Entities;
using CTCare.Domain.Primitives;

using Microsoft.EntityFrameworkCore;

namespace CTCare.Infrastructure.Persistence;

public class CtCareDbContext(DbContextOptions<CtCareDbContext> opt): DbContext(opt)
{
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<LeavePolicy> LeavePolicies => Set<LeavePolicy>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<LeaveApprovalStep> LeaveApprovalSteps => Set<LeaveApprovalStep>();
    public DbSet<LeaveDocument> LeaveDocuments => Set<LeaveDocument>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<User> UserAccounts => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<UserRole>(a =>
        {
            a.HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            a.HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.ApplyConfigurationsFromAssembly(typeof(CtCareDbContext).Assembly);

        // Global soft delete query filter to enforce and convert hard deletes to soft-delete for any entity that has IsDeleted
        foreach (var et in b.Model.GetEntityTypes())
        {
            if (et.IsOwned())
            {
                continue;
            }

            var hasIsDeleted = et.FindProperty("IsDeleted") != null || et.ClrType.GetProperty("IsDeleted") != null;
            if (!hasIsDeleted)
            {
                continue;
            }

            var param = Expression.Parameter(et.ClrType, "e");
            var efProp = typeof(EF).GetMethod(nameof(EF.Property))!.MakeGenericMethod(typeof(bool?));
            var isDeleted = Expression.Call(efProp, param, Expression.Constant("IsDeleted"));
            var coalesceFalse = Expression.Coalesce(isDeleted, Expression.Constant(false, typeof(bool)));
            var notDeleted = Expression.Equal(coalesceFalse, Expression.Constant(false));
            var softDeleteFilter = Expression.Lambda(notDeleted, param);

            var eb = b.Entity(et.ClrType);

            var existing = eb.Metadata.GetQueryFilter();
            if (existing is not null)
            {
                var rebased = RebindParameter(existing.Body, existing.Parameters[0], param);
                var combined = Expression.AndAlso(rebased, softDeleteFilter.Body);
                eb.HasQueryFilter(Expression.Lambda(combined, param));
            }
            else
            {
                eb.HasQueryFilter(softDeleteFilter);
            }
        }

        // avoid hard cascade deletes; let interceptor/SaveChanges convert to soft-delete
        foreach (var fk in b.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            if (fk.IsOwnership)
            {
                continue;
            }

            fk.DeleteBehavior = DeleteBehavior.Restrict;
        }

        base.OnModelCreating(b);
    }

    // Helper to swap the parameter in an expression so two lambdas can be combined
    private static Expression RebindParameter(Expression body, ParameterExpression from, ParameterExpression to) => new ParamReBinder(from, to).Visit(body)!;

    private sealed class ParamReBinder(ParameterExpression from, ParameterExpression to): ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditingAndSoftDelete();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditingAndSoftDelete();
        return base.SaveChanges();
    }

    private void ApplyAuditingAndSoftDelete()
    {
        var utcNow = DateTimeOffset.UtcNow;

        // Enforce soft-delete: convert hard deletes into updates that set IsDeleted = true
        foreach (var e in ChangeTracker.Entries().Where(e => e.State == EntityState.Deleted))
        {
            var hasIsDeleted = e.Metadata.FindProperty("IsDeleted") != null || e.Entity.GetType().GetProperty("IsDeleted") != null;
            if (!hasIsDeleted)
            {
                continue;
            }

            e.State = EntityState.Modified;
            e.CurrentValues["IsDeleted"] = true;

            if (e.Metadata.FindProperty("DeletedAt") != null)
            {
                e.CurrentValues["DeletedAt"] = DateTimeOffset.UtcNow;
            }

            if (e.Metadata.FindProperty("UpdatedAt") != null)
            {
                e.CurrentValues["UpdatedAt"] = utcNow;
            }
        }

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = utcNow;
                entry.Entity.UpdatedAt = utcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = utcNow;
            }
        }

        foreach (var e in ChangeTracker.Entries<User>())
        {
            if (e.State is EntityState.Added or EntityState.Modified)
            {
                e.Entity.Email = (e.Entity.Email ?? string.Empty).Trim().ToLowerInvariant();
            }
        }
    }
}
