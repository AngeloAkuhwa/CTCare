using CTCare.Application.Leaves.Abstractions;

namespace CTCare.Infrastructure.Leave.Calendar;

public sealed class BusinessCalendarService(IHolidayProvider holidays): IBusinessCalendarService
{
    public bool IsWorkingDay(DateOnly date)
    {
        var dow = date.DayOfWeek;
        if (dow is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return false;
        }

        return !holidays.IsHoliday(date);
    }

    public IEnumerable<DateOnly> EnumerateBusinessDaysInclusive(DateOnly start, DateOnly end)
    {
        if (end < start)
        {
            yield break;
        }

        for (var d = start; d <= end; d = d.AddDays(1))
        {
            if (IsWorkingDay(d))
            {
                yield return d;
            }
        }
    }

    public int CountBusinessDaysInclusive(DateOnly start, DateOnly end)
        => EnumerateBusinessDaysInclusive(start, end).Count();
}
