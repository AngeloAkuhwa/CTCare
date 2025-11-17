using CTCare.Application.Leaves.Abstractions;

namespace CTCare.Infrastructure.Leave.Calendar;

// TODO: improve this implementation

public sealed class InMemoryHolidayProvider(IEnumerable<DateOnly>? holidays = null): IHolidayProvider
{
    private readonly HashSet<DateOnly> _holidays = [..holidays ?? Enumerable.Empty<DateOnly>()];

    public bool IsHoliday(DateOnly date) => _holidays.Contains(date);
}
