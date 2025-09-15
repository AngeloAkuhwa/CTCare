using Microsoft.EntityFrameworkCore;
using CTCare.Domain.Entities;
using System.Linq.Expressions;

using CTCare.Domain.Primitives;
using Org.BouncyCastle.Utilities.IO;

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


    protected override void OnModelCreating(ModelBuilder b)
    {
        b.ApplyConfigurationsFromAssembly(typeof(CtCareDbContext).Assembly);

        // Globals
        foreach (var entity in b.Model.GetEntityTypes())
        {
            // Soft delete filter
            if (entity.ClrType.GetProperty("IsDeleted") != null)
            {
                var param = Expression.Parameter(entity.ClrType, "e");
                var prop = Expression.Property(param, "IsDeleted");
                var body = Expression.Equal(prop, Expression.Constant(false));
                var lambda = Expression.Lambda(body, param);
                b.Entity(entity.ClrType).HasQueryFilter(lambda);
            }
        }

        base.OnModelCreating(b);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditing();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditing();
        return base.SaveChanges();
    }

    private void ApplyAuditing()
    {
        var entries = ChangeTracker.Entries<BaseEntity>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}
