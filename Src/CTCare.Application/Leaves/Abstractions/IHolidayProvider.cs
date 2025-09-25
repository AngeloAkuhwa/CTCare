namespace CTCare.Application.Leaves.Abstractions;

public interface IHolidayProvider
{
    bool IsHoliday(DateOnly date);
}
