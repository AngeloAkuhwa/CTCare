namespace CTCare.Shared.Settings;

public sealed class LeaveRulesSettings
{
    public string GrantMode { get; set; }
    public bool CarryoverAllowed { get; set; }
    public string AllowedIncrements { get; set; }
    public int DoctorsNoteThresholdConsecutiveDays { get; set; }
    public bool AllowRejection { get; set; }
    public decimal SickLeaveBalance { get; set; }
    public decimal AnnualLeaveBalance { get; set; }
    public decimal SickLeaveDays { get; set; }
    public decimal AnnualLeaveDays { get; set; }
}
