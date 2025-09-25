using CTCare.Domain.Entities;
using CTCare.Infrastructure.Persistence;
using CTCare.Shared.Settings;
using CTCare.Shared.Utilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CTCare.Infrastructure.Leave.Jobs;

public sealed class AnnualEntitlementProvisioner(
    CtCareDbContext db,
    ILogger<AnnualEntitlementProvisioner> log,
    IOptions<LeaveRulesSettings> leaveRules)
{
    /// <summary>
    /// Ensure every active employee has a LeaveBalance row for the year.
    /// If LeaveTypeId is null here, you’re tracking a single “Annual Leave” bucket.
    /// </summary>
    public async Task ProvisionForYearAsync(int year, Guid? leaveTypeId = null, CancellationToken ct = default)
    {
        // Load employees (filter to active if you track status)
        var employees = await db.Employees
            .AsNoTracking()
            .Select(e => e.Id)
            .ToListAsync(ct);

        // Fetch existing balances for quick lookups
        var existing = await db.LeaveBalances
            .AsNoTracking()
            .Where(b => b.Year == year && b.LeaveTypeId == leaveTypeId)
            .Select(b => new { b.EmployeeId })
            .ToListAsync(ct);

        var existingSet = existing.Select(x => x.EmployeeId).ToHashSet();
        var toAdd = new List<LeaveBalance>();

        foreach (var empId in employees)
        {
            if (existingSet.Contains(empId))
            {
                continue;
            }

            toAdd.Add(new LeaveBalance
            {
                Id = SequentialGuid.NewGuid(),
                EmployeeId = empId,
                LeaveTypeId = leaveTypeId,
                Year = year,
                EntitledDays = leaveRules.Value.SickLeaveDays,
                UsedDays = 0,
                PendingDays = 0
            });
        }

        if (toAdd.Count == 0)
        {
            log.LogInformation("LeaveBalance provisioning: nothing to add for {Year}.", year);
            return;
        }

        db.LeaveBalances.AddRange(toAdd);
        await db.SaveChangesAsync(ct);
        log.LogInformation("LeaveBalance provisioning: created {Count} rows for {Year}.", toAdd.Count, year);
    }
}
