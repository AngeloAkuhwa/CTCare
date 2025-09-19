//// Application/Leaves/SubmitLeave.cs
//using CTCare.Domain.Entities;
//using CTCare.Domain.Enums;
//using CTCare.Shared.Settings;

//using MediatR;

//using Microsoft.Extensions.Options;

//public static class SubmitLeave
//{
//    public record Command(
//        Guid EmployeeId,
//        Guid LeaveTypeId,
//        DateOnly Start,
//        DateOnly End,
//        bool IsHalfDay,
//        string Reason,
//        IReadOnlyList<(string FileName, string ContentType, byte[] Bytes, DocumentKind Kind)>? Docs
//    ): IRequest<Result>;

//    public record Result(Guid LeaveRequestId, string Status);

//    public class Handler(
//        CtCareDbContext db,
//        IOptions<LeaveRulesSettings> rulesOpt
//    ): IRequestHandler<Command, Result>
//    {
//        private readonly LeaveRulesSettings _rules = rulesOpt.Value;

//        public async Task<Result> Handle(Command req, CancellationToken ct)
//        {
//            // Load per-type policy + employee
//            var policy = await db.LeavePolicies
//                .Include(p => p.LeaveType)
//                .FirstOrDefaultAsync(p => p.LeaveTypeId == req.LeaveTypeId, ct)
//                ?? throw new InvalidOperationException("No policy for selected leave type.");

//            var emp = await db.Employees.Include(e => e.Manager)
//                .FirstOrDefaultAsync(e => e.Id == req.EmployeeId, ct)
//                ?? throw new InvalidOperationException("Employee not found.");

//            // Calculate requested days (supports half day for single-day)
//            var requested = CalculateRequestedDays(req.Start, req.End, req.IsHalfDay);

//            // Validate increments from config (not hardcoded)
//            EnsureIncrementAllowed(requested, _rules.AllowedIncrements);

//            // Balance: Upfront + no carryover => entitlement - approved_this_year
//            var remaining = await GetRemainingForYearAsync(db, emp.Id, req.LeaveTypeId, req.Start.Year, ct, policy);
//            if (requested > remaining)
//                throw new InvalidOperationException($"Insufficient balance. Remaining: {remaining} day(s).");

//            // Doctor’s note threshold (> 2 consecutive days => threshold=3)
//            var consecutive = (req.End.DayNumber - req.Start.DayNumber) + 1;
//            var needDoc = consecutive >= _rules.DoctorsNoteThresholdConsecutiveDays;
//            if (needDoc)
//            {
//                var hasNote = req.Docs?.Any(d => d.Kind == DocumentKind.DoctorsNote) ?? false; // enum exists
//                if (!hasNote) throw new InvalidOperationException("Doctor's note required.");
//            }

//            // Create leave request
//            var leave = new LeaveRequest
//            {
//                EmployeeId = emp.Id,
//                LeaveTypeId = req.LeaveTypeId,
//                StartDate = req.Start.ToDateTime(TimeOnly.MinValue),
//                EndDate = req.End.ToDateTime(TimeOnly.MinValue),
//                Reason = req.Reason,
//                Status = LeaveStatus.Pending,
//                TotalDays = requested
//            };

//            if (req.Docs is { Count: > 0 })
//            {
//                leave.Documents = req.Docs.Select(d => new LeaveDocument
//                {
//                    FileName = d.FileName,
//                    ContentType = d.ContentType,
//                    StoragePath = "", // your blob path (to be populated by uploader)
//                    Kind = d.Kind
//                }).ToList();
//            }

//            if (policy.RequiresManagerApproval)
//            {
//                if (emp.ManagerId is null)
//                    throw new InvalidOperationException("No manager assigned for approval.");

//                leave.ApprovalFlow =
//                [
//                    new LeaveApprovalStep
//                    {
//                        StepNumber = 1,
//                        ApproverId = emp.ManagerId.Value,
//                        Status = LeaveStatus.Pending
//                    }
//                ];
//            }

//            db.LeaveRequests.Add(leave);
//            await db.SaveChangesAsync(ct);
//            return new Result(leave.Id, leave.Status.ToString());
//        }

//        private static decimal CalculateRequestedDays(DateOnly start, DateOnly end, bool halfDay)
//        {
//            if (end < start) throw new ArgumentException("End before start.");
//            var total = (end.DayNumber - start.DayNumber) + 1;
//            if (halfDay && total == 1) return 0.5m;
//            return total;
//        }

//        private static void EnsureIncrementAllowed(decimal requested, string allowed)
//        {
//            var ok = allowed switch
//            {
//                "FullDay" => requested % 1m == 0m,
//                "FullOrHalfDay" => requested % 0.5m == 0m,
//                _ => false
//            };
//            if (!ok) throw new InvalidOperationException("Requested increment not allowed.");
//        }

//        private static async Task<decimal> GetRemainingForYearAsync(
//            CtCareDbContext db,
//            Guid employeeId,
//            Guid leaveTypeId,
//            int year,
//            CancellationToken ct,
//            LeavePolicy policy)
//        {
//            // Sum of approved in current year
//            var used = await db.LeaveRequests.AsNoTracking()
//                .Where(r => r.EmployeeId == employeeId
//                         && r.LeaveTypeId == leaveTypeId
//                         && r.Status == LeaveStatus.Approved
//                         && r.StartDate.Year == year)
//                .SumAsync(r => (decimal)r.TotalDays, ct);

//            return Math.Max(0m, policy.MaxDaysPerYear - used);
//        }
//    }
//}
