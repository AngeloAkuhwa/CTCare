using System.Globalization;

using CTCare.Domain.Entities;
using CTCare.Domain.Enums;
using CTCare.Infrastructure.Security;
using CTCare.Shared.Utilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CTCare.Infrastructure.Persistence;

public static class DbSeed
{
    public static async Task SeedAsync(CtCareDbContext db, IServiceProvider sp)
    {
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeed");
        var cfg = sp.GetRequiredService<IConfiguration>();
        var env = sp.GetRequiredService<IHostEnvironment>();
        var hasher = sp.GetRequiredService<IPasswordHasher>();

        await db.Database.MigrateAsync();

        await using var tx = await db.Database.BeginTransactionAsync();

        try
        {
            // Departments
            var departmentsToEnsure = new (string Code, string Name, string? Description)[]
            {
                ("ENG", "Engineering", "Engineering & Platform"),
                ("OPS", "Operations", "Ops & Support"),
                ("FIN", "Finance", "Finance & Accounting"),
                ("HR", "Human Resources", "People & Culture"),
                ("CLN", "Clinical", "Clinical Programs")
            };

            var deptMap = new Dictionary<string, Department>(StringComparer.OrdinalIgnoreCase);
            foreach (var (code, name, desc) in departmentsToEnsure)
            {
                var dept = await db.Departments.FirstOrDefaultAsync(d => d.Code == code);
                if (dept is null)
                {
                    dept = new Department { Code = code, Name = name, Description = desc };
                    db.Departments.Add(dept);
                    logger.LogInformation("Seed: Department {Code} created.", code);
                }
                else
                {
                    if (dept.Name != name || dept.Description != desc)
                    {
                        dept.Name = name;
                        dept.Description = desc;
                        logger.LogInformation("Seed: Department {Code} updated.", code);
                    }
                }

                deptMap[code] = dept;
            }

            // Teams (nested under Departments)
            // Engineering sub_teams
            var teamsByDept = new Dictionary<string, (string Code, string Name, string? Desc)[]>
            {
                ["ENG"] =
                [
                    ("HSP", "Hospice", "Engineering - Hospice"),
                    ("HHC", "Home Health", "Engineering - Home Health"),
                    ("HCR", "Home Care", "Engineering - Home Care"),
                    ("PLT", "Platform", "Platform & Shared Services"),
                    ("INT", "Integrations", "Integrations & Data")
                ],
                ["OPS"] =
                [
                    ("SOP", "Support Ops", "Support & Operations"),
                    ("NOC", "NOC", "Network operations center")
                ],
                ["FIN"] =
                [
                    ("ACT", "Accounting", "Accounting & AP/AR"),
                    ("REV", "Revenue", "Billing & Revenue")
                ],
                ["HR"] =
                [
                    ("TAL", "Talent", "Talent & Recruiting"),
                    ("BEN", "Benefits", "Comp & Benefits")
                ],
                ["CLN"] =
                [
                    ("CQA", "Clinical QA", "Clinical QA"),
                    ("EDU", "Education", "Clinician Education")
                ]
            };

            var teamMap = new Dictionary<(string DeptCode, string TeamCode), Team>();

            foreach (var kv in teamsByDept)
            {
                if (!deptMap.TryGetValue(kv.Key, out var dept))
                {
                    continue;
                }

                foreach (var (code, name, desc) in kv.Value)
                {
                    var existing = await db.Teams.FirstOrDefaultAsync(t =>
                        t.DepartmentId == dept.Id && t.Code == code);

                    if (existing is null)
                    {
                        existing = new Team
                        {
                            DepartmentId = dept.Id,
                            Code = code,
                            Name = name,
                            Description = desc
                        };
                        db.Teams.Add(existing);
                        logger.LogInformation("Seed: Team {Dept}:{Code} created.", dept.Code, code);
                    }
                    else
                    {
                        if (existing.Name != name || existing.Description != desc)
                        {
                            existing.Name = name;
                            existing.Description = desc;
                            logger.LogInformation("Seed: Team {Dept}:{Code} updated.", dept.Code, code);
                        }
                    }

                    teamMap[(dept.Code, code)] = existing;
                }
            }

            // LeaveTypes
            var leaveTypes = new (string Name, bool IsPaid, int MaxDays, string? Desc)[]
            {
                ("Annual", true, 30, "Annual paid leave"),
                ("Sick", true, 10, "Sick/Medical leave"),
                ("Maternity", true, 90, "Maternity leave"),
                ("Paternity", true, 10, "Paternity leave"),
                ("Bereavement", true, 5, "Bereavement leave"),
                ("Unpaid", false, 365, "Unpaid leave")
            };

            foreach (var (name, paid, max, desc) in leaveTypes)
            {
                var lt = await db.LeaveTypes.FirstOrDefaultAsync(x => x.Name == name);
                if (lt is null)
                {
                    db.LeaveTypes.Add(new LeaveType
                    {
                        Name = name,
                        IsPaid = paid,
                        MaxDaysPerYear = max,
                        Description = desc
                    });

                    logger.LogInformation("Seed: LeaveType {Name} created.", name);
                }
                else
                {
                    var changed = false;

                    if (lt.IsPaid != paid)
                    {
                        lt.IsPaid = paid;
                        changed = true;
                    }

                    if (lt.MaxDaysPerYear != max)
                    {
                        lt.MaxDaysPerYear = max;
                        changed = true;
                    }

                    if (lt.Description != desc)
                    {
                        lt.Description = desc;
                        changed = true;
                    }

                    if (changed)
                    {
                        logger.LogInformation("Seed: LeaveType {Name} updated.", name);
                    }
                }
            }

            // Roles
            if (!await db.Roles.AnyAsync())
            {
                var roles = Enum.GetValues<UserRoles>().Select(r => new Role
                {
                    Id = SequentialGuid.NewGuid(),
                    Name = r.ToString(),
                    Description = $"System role: {r}"
                }).ToList();

                db.Roles.AddRange(roles);
                logger.LogInformation("Seed: Default roles created.");

                await db.SaveChangesAsync();
            }


            // Sample Employees + Manager relationship
            var platTeam = teamMap[("ENG", "PLT")];

            var managerEmail = "manager@gmail.com";
            var devEmail = "dev@gmail.com";

            var manager = await db.Employees.FirstOrDefaultAsync(e => e.Email == managerEmail);
            if (manager is null)
            {
                manager = new Employee
                {
                    EmployeeCode = await NextEmployeeCodeAsync(db),
                    Status = EmploymentStatus.Active,
                    EmployeeType = EmployeeType.FullTime,
                    DateOfHire = DateTimeOffset.UtcNow,
                    AnnualLeaveDays = 30,
                    SickLeaveDays = 10,
                    AnnualLeaveBalance = 30,
                    SickLeaveBalance = 10,
                    Team = platTeam,
                    Designation = "Engineering Manager",
                    Sex = Gender.Male,
                    Email = managerEmail,
                    EmailStatus = EmailStatus.Verified,
                    DepartmentId = platTeam.DepartmentId,
                    FirstName = "John",
                    LastName = "Doe"
                };

                db.Employees.Add(manager);
                await db.SaveChangesAsync();
            }

            var dev = await db.Employees.FirstOrDefaultAsync(e => e.Email == devEmail);
            if (dev is null)
            {
                dev = new Employee
                {
                    EmployeeCode = await NextEmployeeCodeAsync(db),
                    Status = EmploymentStatus.Active,
                    EmployeeType = EmployeeType.FullTime,
                    DateOfHire = DateTimeOffset.UtcNow,
                    AnnualLeaveDays = 30,
                    SickLeaveDays = 10,
                    AnnualLeaveBalance = 30,
                    SickLeaveBalance = 10,
                    Team = platTeam,
                    Designation = "Software Developer",
                    Sex = Gender.Male,
                    Email = devEmail,
                    EmailStatus = EmailStatus.Verified,
                    DepartmentId = platTeam.DepartmentId,
                    Manager = manager,
                    FirstName = "John",
                    LastName = "Doe"
                };
                db.Employees.Add(dev);
                await db.SaveChangesAsync();
            }

            var (hash, salt) = hasher.Hash("$Angelo123$$##$Angelo123$$##");

            // Users + UserRoles
            if (!await db.UserAccounts.AnyAsync(u => u.Email == managerEmail))
            {
                var user = new User
                {
                    Email = managerEmail,
                    EmailConfirmed = true,
                    PasswordHash = hash,
                    PasswordSalt = salt
                };

                db.UserAccounts.Add(user);

                var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == UserRoles.EngineeringManager.ToString());

                db.UserRoles.Add(new UserRole { User = user, Role = role });
            }

            if (!await db.UserAccounts.AnyAsync(u => u.Email == devEmail))
            {
                var user = new User
                {
                    Email = devEmail,
                    EmailConfirmed = true,
                    PasswordHash = hash,
                    PasswordSalt = salt
                };

                db.UserAccounts.Add(user);

                var role = await db.Roles.FirstAsync(r => r.Name == UserRoles.SoftwareDeveloper.ToString());
                db.UserRoles.Add(new UserRole { User = user, Role = role });
            }

            // API Keys
            // Prefer reading the first configured API key from configuration (Auth:ApiKeys:0)
            var configuredFirstKey = cfg.GetSection("Api:ApiKeys").Get<string[]>()?.FirstOrDefault();
            if (!await db.ApiKeys.AnyAsync())
            {
                var rawKey = string.IsNullOrWhiteSpace(configuredFirstKey)
                    ? "local-dev-key-123"
                    : configuredFirstKey;

                db.ApiKeys.Add(new ApiKey
                {
                    Name = string.IsNullOrWhiteSpace(configuredFirstKey) ? "Local Dev Key" : "Configured Key",
                    Prefix = ApiKeyUtilities.GetPrefix(rawKey),
                    Hash = ApiKeyUtilities.Hash(rawKey),
                    ExpiresAt = DateTimeOffset.MaxValue.UtcDateTime,
                    IsDeleted = false,
                    LastUsedAt = DateTimeOffset.UtcNow,
                });

                logger.LogInformation("Seed: API key created (Prefix={Prefix}).",
                    ApiKeyUtilities.GetPrefix(rawKey));
            }

            var sick = await db.LeaveTypes.FirstAsync(x => x.Name == "Sick");
            var pol = await db.LeavePolicies.FirstOrDefaultAsync(p => p.LeaveTypeId == sick.Id);

            if (pol is null)
            {
                db.LeavePolicies.Add(new LeavePolicy
                {
                    LeaveTypeId = sick.Id,
                    MaxDaysPerYear = 12m,
                    RequiresManagerApproval = true,
                    RequiresHRApproval = false,
                    ApprovalSteps = 1
                });
                logger.LogInformation("Seed: LeavePolicy(Sick)=12d, mgr approval, 1 step.");
            }
            else
            {
                // keep it up to date if changed in config
                var changed = false;
                if (pol.MaxDaysPerYear != 12m)
                {
                    pol.MaxDaysPerYear = 12m;
                    changed = true;
                }

                if (!pol.RequiresManagerApproval)
                {
                    pol.RequiresManagerApproval = true;
                    changed = true;
                }

                if (pol.RequiresHRApproval)
                {
                    pol.RequiresHRApproval = false;
                    changed = true;
                }

                if (pol.ApprovalSteps != 1)
                {

                    pol.ApprovalSteps = 1;
                    changed = true;
                }

                if (changed)
                {
                    logger.LogInformation("Seed: LeavePolicy(Sick) updated.");
                }
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            logger.LogError(ex, "Error during database seed.");
            throw;
        }
    }

    /// <summary>
    /// Generates the next employee code in the form CN00001 but scans existing max first.
    /// </summary>
    private static async Task<string> NextEmployeeCodeAsync(CtCareDbContext db)
    {
        // We scan only codes matching CN\\d{5}; you can replace with a persistent sequence if preferred.
        var maxNumeric = 0;

        var existing = await db.Employees
            .Select(e => e.EmployeeCode)
            .Where(c => c.Length == 7)
            .ToListAsync();

        foreach (var code in existing)
        {
            if (int.TryParse(code.AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out var n))
            {
                maxNumeric = Math.Max(maxNumeric, n);
            }
        }

        var next = maxNumeric + 1;
        return $"CN{next.ToString("D5", CultureInfo.InvariantCulture)}";
    }
}
