using CTCare.Domain.Enums;

namespace CTCare.Application.Leaves.Abstractions;

public interface IDoctorsNoteRule
{
    /// <summary>Doctor’s note required iff consecutive business days &gt; 2 (half-day never triggers).</summary>
    bool RequiresDoctorNote(DateOnly start, DateOnly end, LeaveUnit unit, IBusinessCalendarService calendar);
}
