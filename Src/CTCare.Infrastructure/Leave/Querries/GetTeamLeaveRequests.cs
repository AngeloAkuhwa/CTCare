using System.Linq.Expressions;

using CTCare.Application.Leaves;
using CTCare.Domain.Entities;
using CTCare.Domain.Enums;
using CTCare.Infrastructure.Extensions;
using CTCare.Infrastructure.Persistence;
using CTCare.Shared.Interfaces;

using MediatR;

using Microsoft.EntityFrameworkCore;

namespace CTCare.Infrastructure.Leave.Querries
{
    public static class GetTeamLeaveRequests
    {
        public sealed class Query: IRequest<PagedResult<TeamLeaveItemInfo>>
        {
            public Guid ManagerId { get; set; }
            public TeamLeaveFilterRequest Filter { get; set; }
        }

        public sealed class Handler(CtCareDbContext db): IRequestHandler<Query, PagedResult<TeamLeaveItemInfo>>
        {
            public async Task<PagedResult<TeamLeaveItemInfo>> Handle(Query req, CancellationToken ct)
            {
                var filter = req.Filter ?? new TeamLeaveFilterRequest();

                var statuses = ParseStatuses(filter.StatusesCsv);

                IQueryable<LeaveRequest> q = db.LeaveRequests
                    .AsNoTracking()
                    .Include(x => x.Employee);

                q = q.Where(x =>
                        x.ManagerId == req.ManagerId ||
                        (x.Employee.ManagerId != null && x.Employee.ManagerId == req.ManagerId));

                if (statuses.Count > 0)
                {
                    q = q.Where(x => statuses.Contains(x.Status));
                }

                if (filter.From is { } from)
                {
                    q = q.Where(x => x.StartDate >= from);
                }

                if (filter.To is { } to)
                {
                    q = q.Where(x => x.EndDate <= to);
                }

                q = q.OrderByDescending(x => x.CreatedAt);

                // Build a single EF-translatable selector expression
                Expression<Func<LeaveRequest, TeamLeaveItemInfo>> selector =
                    x => new TeamLeaveItemInfo
                    {
                        RequestId = x.Id,
                        EmployeeId = x.EmployeeId,
                        EmployeeName = x.Employee.FirstName + " " + x.Employee.LastName,

                        DepartmentId = x.Employee.DepartmentId,
                        DepartmentName = db.Departments
                                              .Where(d => d.Id == x.Employee.DepartmentId)
                                              .Select(d => d.Name)
                                              .FirstOrDefault(),

                        TeamId = x.Employee.TeamId,
                        TeamName = db.Teams
                                              .Where(t => t.Id == x.Employee.TeamId)
                                              .Select(t => t.Name)
                                              .FirstOrDefault(),

                        ManagerId = x.ManagerId ?? x.Employee.ManagerId,
                        ManagerName = db.Employees
                                              .Where(m => m.Id == (x.ManagerId ?? x.Employee.ManagerId))
                                              .Select(m => m.FirstName + " " + m.LastName)
                                              .FirstOrDefault(),

                        LeaveTypeId = x.LeaveTypeId,
                        LeaveTypeName = db.LeaveTypes
                                              .Where(t => t.Id == x.LeaveTypeId)
                                              .Select(t => t.Name)
                                              .FirstOrDefault(),

                        StartDate = x.StartDate,
                        EndDate = x.EndDate,
                        DaysRequested = x.DaysRequested,
                        Unit = x.Unit.ToString(),
                        Status = x.Status.ToString(),
                        HasDoctorNote = x.HasDoctorNote,
                        EmployeeComment = x.EmployeeComment,
                        ManagerComment = x.ManagerComment,
                        CreatedAt = x.CreatedAt
                    };

                var paged = await q.ToPagedResultAsync(
                    request: filter,
                    selector: selector,
                    maxPageLength: filter.PageLength,
                    ct: ct);

                return paged;
            }

            private static IReadOnlyCollection<LeaveStatus> ParseStatuses(string? csv)
            {
                if (string.IsNullOrWhiteSpace(csv))
                {
                    return Array.Empty<LeaveStatus>();
                }

                var names = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var list = new List<LeaveStatus>(names.Length);

                foreach (var n in names)
                {
                    if (Enum.TryParse<LeaveStatus>(n, ignoreCase: true, out var s))
                    {
                        list.Add(s);
                    }
                }

                return list;
            }
        }
    }
}
