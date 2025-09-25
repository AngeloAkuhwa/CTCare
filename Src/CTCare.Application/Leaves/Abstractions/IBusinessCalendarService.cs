namespace CTCare.Application.Leaves.Abstractions;

public interface IBusinessCalendarService
{
    bool IsWorkingDay(DateOnly date);
    IEnumerable<DateOnly> EnumerateBusinessDaysInclusive(DateOnly start, DateOnly end);
    int CountBusinessDaysInclusive(DateOnly start, DateOnly end);
}
