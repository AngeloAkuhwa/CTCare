using CTCare.Application.Leaves.Abstractions;
using CTCare.Domain.Enums;

namespace CTCare.Infrastructure.Leave.Rules;

public sealed class DoctorsNoteRule: IDoctorsNoteRule
{
    // TODO: pull this from db always
    private const int MaxDays = 2;
    public bool RequiresDoctorNote(DateOnly start, DateOnly end, LeaveUnit unit, IBusinessCalendarService cal)
    {
        if (unit == LeaveUnit.HalfDay)
        {
            return false;
        }

        var days = cal.CountBusinessDaysInclusive(start, end);
        return days > MaxDays;
    }
}
