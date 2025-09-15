using System.Globalization;

using CTCare.Domain.Entities;
using CTCare.Domain.Enums;
using CTCare.Infrastructure.Security;

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

        await db.Database.MigrateAsync();

        await using var tx = await db.Database.BeginTransactionAsync();

        try
        {
            // 1. Departments
            var departmentsToEnsure = new (string Code, string Name, string? Description)[]
            {
                ("ENG", "Engineering", "Engineering & Platform"),
                ("OPS", "Operations", "Ops & Support"),
                ("FIN", "Finance", "Finance & Accounting"),
                ("HR",  "Human Resources", "People & Culture"),
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

            // 2. Teams (nested under Departments)
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

            // 3. LeaveTypes
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
                        lt.IsPaid = paid; changed = true;
                    }

                    if (lt.MaxDaysPerYear != max)
                    {
                        lt.MaxDaysPerYear = max; changed = true;
                    }

                    if (lt.Description != desc)
                    {
                        lt.Description = desc; changed = true;
                    }

                    if (changed)
                    {
                        logger.LogInformation("Seed: LeaveType {Name} updated.", name);
                    }
                }
            }

            // 4. Optional sample Employee (dev-only) to validate relations
            if (env.IsDevelopment())
            {
                var eng = deptMap["ENG"];
                var plat = teamMap[(eng.Code, "PLT")];

                var adminEmail = "admin@gmail.com";
                var exists = await db.Employees.AnyAsync(e => e.Email == adminEmail);
                if (!exists)
                {
                    var code = await NextEmployeeCodeAsync(db);
                    var emp = new Employee
                    {
                        EmployeeCode = code,
                        Status = EmploymentStatus.Active,
                        DateOfHire = DateTimeOffset.UtcNow,
                        AnnualLeaveDays = 30,
                        SickLeaveDays = 10,
                        AnnualLeaveBalance = 30,
                        SickLeaveBalance = 10,
                        Team = plat,
                        TeamId = plat.Id,
                        Designation = "Platform Admin",
                        Sex = Gender.Male,
                        Email = adminEmail,
                        EmailStatus = EmailStatus.Verified,
                        DepartmentId = eng.Id
                    };
                    db.Employees.Add(emp);
                    logger.LogInformation("Seed: Dev employee {Email} created.", adminEmail);
                }
            }

            // 5. API Keys
            // Prefer reading the first configured API key from configuration (Auth:ApiKeys:0)
            var configuredFirstKey = cfg.GetSection("Auth:ApiKeys").Get<string[]>()?.FirstOrDefault();
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

                logger.LogInformation("Seed: API key created (Prefix={Prefix}).", ApiKeyUtilities.GetPrefix(rawKey));
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
