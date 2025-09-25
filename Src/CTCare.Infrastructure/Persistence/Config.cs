using CTCare.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CTCare.Infrastructure.Persistence
{
    public class DepartmentConfig: IEntityTypeConfiguration<Department>
    {
        public void Configure(EntityTypeBuilder<Department> e)
        {
            e.ToTable("Departments");

            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);

            e.HasIndex(x => x.Name).IsUnique();
            e.HasIndex(x => x.Code).IsUnique();

            e.HasMany(x => x.Teams)
                .WithOne(t => t.Department)
                .HasForeignKey(t => t.DepartmentId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(x => x.Employees)
                .WithOne(emp => emp.Department)
                .HasForeignKey(emp => emp.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class TeamConfig: IEntityTypeConfiguration<Team>
    {
        public void Configure(EntityTypeBuilder<Team> e)
        {
            e.ToTable("Teams");

            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);

            e.HasIndex(x => new { x.DepartmentId, x.Name }).IsUnique();
            e.HasIndex(x => new { x.DepartmentId, x.Code }).IsUnique();

            e.HasMany(x => x.Members)
                .WithOne(emp => emp.Team)
                .HasForeignKey(emp => emp.TeamId)
                .OnDelete(DeleteBehavior.SetNull); // employees can outlive a team rename/removal
        }
    }

    public class EmployeeConfig: IEntityTypeConfiguration<Employee>
    {
        public void Configure(EntityTypeBuilder<Employee> e)
        {
            e.ToTable("Employees");

            e.Property(x => x.EmployeeCode).HasMaxLength(10).IsRequired();
            e.HasIndex(x => x.EmployeeCode).IsUnique();

            e.Property(x => x.Status).HasConversion<string>();

            e.Property(x => x.Designation).HasMaxLength(200).IsRequired();
            e.Property(x => x.Sex).HasConversion<string>().IsRequired();

            e.Property(x => x.Email).HasMaxLength(250).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();

            e.Property(x => x.EmailStatus).HasConversion<string>().IsRequired();

            e.HasOne(x => x.Manager)
                .WithMany(x => x.DirectReports)
                .HasForeignKey(x => x.ManagerId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Department)
                .WithMany(d => d.Employees)
                .HasForeignKey(x => x.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Team)
                .WithMany(t => t.Members)
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(x => new { x.DepartmentId, x.TeamId, x.Status });
        }
    }

    public class LeaveTypeConfig: IEntityTypeConfiguration<LeaveType>
    {
        public void Configure(EntityTypeBuilder<LeaveType> e)
        {
            e.ToTable("LeaveTypes");

            e.Property(x => x.Name)
                .HasMaxLength(150)
                .IsRequired();

            e.Property(x => x.Description)
                .HasMaxLength(1000);

            e.Property(x => x.IsPaid)
                .HasDefaultValue(true);

            e.Property(x => x.MaxDaysPerYear)
                .HasDefaultValue(30);

            e.HasIndex(x => x.Name).IsUnique();
        }
    }

    public class LeaveApprovalStepConfig: IEntityTypeConfiguration<LeaveApprovalStep>
    {
        public void Configure(EntityTypeBuilder<LeaveApprovalStep> e)
        {
            e.ToTable("LeaveApprovalSteps");

            e.Property(x => x.Status).HasConversion<string>();

            e.Property(x => x.Comment).HasMaxLength(2000);

            e.HasIndex(x => new { x.LeaveRequestId, x.StepNumber }).IsUnique();

            e.HasOne(x => x.LeaveRequest)
                .WithMany(x => x.ApprovalFlow)
                .HasForeignKey(x => x.LeaveRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            // Approver is an Employee
            e.HasOne(x => x.Approver)
                .WithMany()
                .HasForeignKey(x => x.ApproverId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class LeaveDocumentConfig: IEntityTypeConfiguration<LeaveDocument>
    {
        public void Configure(EntityTypeBuilder<LeaveDocument> e)
        {
            e.ToTable("LeaveDocuments");

            e.Property(x => x.FileName)
                .HasMaxLength(255)
                .IsRequired();

            e.Property(x => x.ContentType)
                .HasMaxLength(150)
                .IsRequired();

            e.Property(x => x.StoragePath)
                .HasMaxLength(1000);

            e.HasIndex(x => new { x.LeaveRequestId, x.FileName });

            e.HasOne(x => x.LeaveRequest)
                .WithMany(x => x.Documents)
                .HasForeignKey(x => x.LeaveRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class RefreshTokenConfig: IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> e)
        {
            e.ToTable("RefreshTokens");

            e.Property(x => x.Token)
                .HasMaxLength(300)
                .IsRequired();

            e.HasIndex(x => x.Token).IsUnique();

            e.Property(x => x.Revoked).HasDefaultValue(false);
            e.Property(x => x.ExpiresAt).IsRequired();

            e.HasOne<Employee>()
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class AuditLogConfig: IEntityTypeConfiguration<AuditLog>
    {
        public void Configure(EntityTypeBuilder<AuditLog> e)
        {
            e.ToTable("AuditLogs");

            e.HasKey(a => a.Id);
            e.Property(a => a.Id)
                .HasColumnType("uuid")
                .ValueGeneratedOnAdd()
                .HasDefaultValueSql("gen_random_uuid()");

            e.Property(x => x.Action)
                .HasMaxLength(100)
                .IsRequired();

            e.Property(x => x.EntityName)
                .HasMaxLength(200)
                .IsRequired();

            e.Property(x => x.EntityId)
                .HasMaxLength(100)
                .IsRequired();

            e.Property(x => x.ActorName)
                .HasMaxLength(200);

            e.Property(x => x.ActorId) 
                .IsRequired(false);

            e.Property(x => x.ChangedColumns)
                .HasColumnType("text[]");

            e.Property(x => x.TimestampUtc)
                .HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'")
                .ValueGeneratedOnAdd();

            e.Property(x => x.OldValues)
                .HasColumnType("jsonb");

            e.Property(x => x.NewValues)
                .HasColumnType("jsonb");

            e.Property(x => x.Metadata)
                .HasColumnType("jsonb");

            e.HasIndex(x => new { x.EntityName, x.EntityId, x.TimestampUtc });
            e.HasIndex(x => new { x.ActorId, x.TimestampUtc });

            e.HasIndex(x => new { x.EntityName, x.EntityId, x.TimestampUtc });
            e.HasIndex(x => new { x.ActorId, x.TimestampUtc });
        }
    }

    public class LeaveRequestConfig: IEntityTypeConfiguration<LeaveRequest>
    {
        public void Configure(EntityTypeBuilder<LeaveRequest> e)
        {
            e.ToTable("LeaveRequests");

            e.HasKey(x => x.Id);

            e.Property(x => x.StartDate).IsRequired();
            e.Property(x => x.EndDate).IsRequired();

            e.Property(x => x.DaysRequested).HasPrecision(5, 2).IsRequired();
            e.Property(x => x.EmployeeComment).HasMaxLength(1000);
            e.Property(x => x.ManagerComment).HasMaxLength(1000);

            e.Property(x => x.Status).HasConversion<string>();

            e.Property(x => x.Reason).HasMaxLength(2000);

            e.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.LeaveType)
                .WithMany()
                .HasForeignKey(x => x.LeaveTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(x => x.ApprovalFlow)
                .WithOne(a => a.LeaveRequest!)
                .HasForeignKey(a => a.LeaveRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(x => x.Documents)
                .WithOne(d => d.LeaveRequest!)
                .HasForeignKey(d => d.LeaveRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.EmployeeId, x.Status, x.StartDate });
            e.HasIndex(x => new { x.ManagerId, x.Status, x.StartDate });
        }
    }

    public class LeaveBalanceConfig: IEntityTypeConfiguration<LeaveBalance>
    {
        public void Configure(EntityTypeBuilder<LeaveBalance> b)
        {
            b.ToTable("LeaveBalances");
            b.HasKey(x => x.Id);

            b.Property(x => x.EntitledDays).HasPrecision(5, 2).IsRequired();
            b.Property(x => x.UsedDays).HasPrecision(5, 2).IsRequired();
            b.Property(x => x.PendingDays).HasPrecision(5, 2).IsRequired();

            b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.LeaveType).WithMany().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(x => new { x.EmployeeId, x.LeaveTypeId, x.Year }).IsUnique();
        }
    }


    public class LeaveApprovalEventConfig: IEntityTypeConfiguration<LeaveApprovalEvent>
    {
        public void Configure(EntityTypeBuilder<LeaveApprovalEvent> b)
        {
            b.ToTable("LeaveApprovalEvents");
            b.HasKey(x => x.Id);

            b.Property(x => x.Note).HasMaxLength(1000);
            b.HasOne(x => x.LeaveRequest).WithMany().HasForeignKey(x => x.LeaveRequestId).OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(x => new { x.LeaveRequestId, x.CreatedAt });
        }
    }

    public class ApiKeyConfig: IEntityTypeConfiguration<ApiKey>
    {
        public void Configure(EntityTypeBuilder<ApiKey> e)
        {
            e.ToTable("ApiKeys");

            e.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();

            e.Property(x => x.Prefix)
                .HasMaxLength(12)
                .IsRequired();

            e.Property(x => x.Hash)
                .HasMaxLength(256)
                .IsRequired();

            e.Property(x => x.ExpiresAt).IsRequired(false);

            e.HasIndex(x => new { x.Prefix, x.IsDeleted }).IsUnique();
        }
    }

    public class UserAccountConfig: IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> e)
        {
            var emailConverter = new ValueConverter<string, string>(
                v => (v ?? string.Empty).Trim().ToLowerInvariant(),
                v => v
            );

            e.ToTable("UserAccounts");
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(320)
                .HasConversion(emailConverter);
            e.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
            e.Property(x => x.PasswordSalt).HasMaxLength(256).IsRequired();

            e.HasOne(x => x.Employee)
                 .WithOne(emp => emp.User) 
                .HasForeignKey<User>(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
