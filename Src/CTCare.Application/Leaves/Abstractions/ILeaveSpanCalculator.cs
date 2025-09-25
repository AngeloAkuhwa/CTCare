using CTCare.Domain.Enums;

namespace CTCare.Application.Leaves.Abstractions;

public interface ILeaveSpanCalculator
{
    /// <summary>Returns 0.5/1/N business-days according to unit & calendar. Throws if invalid (e.g., half-day across multiple days).</summary>
    decimal ComputeUnits(DateOnly start, DateOnly end, LeaveUnit unit);
}
