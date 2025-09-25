using CTCare.Application.Leaves.Abstractions;
using CTCare.Domain.Enums;

namespace CTCare.Infrastructure.Leave.Calculators;

public sealed class LeaveSpanCalculator(IBusinessCalendarService cal): ILeaveSpanCalculator
{
    public decimal ComputeUnits(DateOnly start, DateOnly end, LeaveUnit unit)
    {
        if (end < start)
        {
            throw new ArgumentException("End date must be ≥ start date.");
        }

        if (unit == LeaveUnit.HalfDay)
        {
            if (start != end)
            {
                throw new InvalidOperationException("Half-day can only be requested for a single date.");
            }

            return cal.IsWorkingDay(start) ? 0.5m : 0m;
        }

        var days = cal.CountBusinessDaysInclusive(start, end);
        return (decimal)days;
    }
}
