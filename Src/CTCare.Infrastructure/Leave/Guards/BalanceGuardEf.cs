using CTCare.Application.Leaves.Abstractions;
using CTCare.Domain.Entities;
using CTCare.Infrastructure.Persistence;
using CTCare.Shared.Constants;
using CTCare.Shared.Utilities;

using Microsoft.EntityFrameworkCore;

namespace CTCare.Infrastructure.Leave.Guards;

public sealed class BalanceGuardEf(CtCareDbContext db): IBalanceGuard
{
    public async Task ReserveAsync(Guid employeeId, int year, decimal units, Guid? leaveTypeId, CancellationToken ct)
    {
        var bal = await db.LeaveBalances
            .SingleOrDefaultAsync(b => b.EmployeeId == employeeId
                                       && b.Year == year
                                       && b.LeaveTypeId == leaveTypeId, ct);

        if (bal is null)
        {
            var already = await db.LeaveBalances.AnyAsync(b => b.EmployeeId == employeeId && b.LeaveTypeId == null && b.Year == year, ct);

            if (!already)
            {
                db.LeaveBalances.Add(new LeaveBalance
                {
                    Id = SequentialGuid.NewGuid(),
                    EmployeeId = employeeId,
                    LeaveTypeId = Guid.Parse(LeaveTypeConstants.SickLeaveTypeConstant),
                    Year = year,
                    EntitledDays = LeaveTypeConstants.SickLeaveTypeEntitlement,
                    UsedDays = 0,
                    PendingDays = units,
                    CreatedBy = employeeId
                });

                return;
            }

            throw new InvalidOperationException("Annual leave balance not provisioned for this year. Contact admin.");
        }

        if (units <= 0)
        {
            throw new InvalidOperationException("Requested units must be greater than zero.");
        }

        var available = bal.EntitledDays - bal.UsedDays - bal.PendingDays;
        if (available < units)
        {
            throw new InvalidOperationException($"Insufficient balance. Available: {available}, requested: {units}.");
        }

        bal.PendingDays += units; 
    }

    public Task EnsureCanApproveAsync(LeaveBalance balance, decimal units, CancellationToken ct)
    {
        if (balance is null)
        {
            throw new ArgumentNullException(nameof(balance));
        }

        if (units <= 0)
        {
            throw new InvalidOperationException("Units must be greater than zero.");
        }

        // Must have reserved at least the requested units
        if (balance.PendingDays < units)
        {
            throw new InvalidOperationException(
                $"Not enough pending units reserved to approve. Pending: {balance.PendingDays}, requested: {units}.");
        }

        // Approving must not exceed entitlement
        var usedAfter = balance.UsedDays + units;
        if (usedAfter > balance.EntitledDays)
        {
            throw new InvalidOperationException(
                $"Approval exceeds entitlement. Entitled: {balance.EntitledDays}, used after approval: {usedAfter}.");
        }

        // ok
        return Task.CompletedTask;
    }
}
