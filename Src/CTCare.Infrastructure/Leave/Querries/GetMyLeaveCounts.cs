using System.Net;
using System.Text.Json;

using CTCare.Application.Interfaces;
using CTCare.Domain.Enums;
using CTCare.Infrastructure.Persistence;
using CTCare.Shared.BasicResult;
using CTCare.Shared.Settings;

using MediatR;

using Microsoft.EntityFrameworkCore;

namespace CTCare.Infrastructure.Leave.Querries
{
    public static class GetMyLeaveCounts
    {
        public sealed class Query: IRequest<Result>
        {
            public Guid EmployeeId { get; set; }
        }

        public sealed class Result: BasicActionResult
        {
            public Result(HttpStatusCode status) : base(status) { }
            public Result(string error) : base(error) { }
            public MyLeaveCountsInfo Data { get; init; } = new();
        }


        public sealed class MyLeaveCountsInfo
        {
            public int Submitted { get; set; }
            public int Returned { get; set; }
            public int Approved { get; set; }
            public int Cancelled { get; set; }
        }
        public sealed class Handler(CtCareDbContext db, ICacheService cache): IRequestHandler<Query, Result>
        {
            public async Task<Result> Handle(Query request, CancellationToken ct)
            {
                if (request.EmployeeId == Guid.Empty)
                {
                    return new Result(HttpStatusCode.BadRequest) { ErrorMessage = "Invalid employee context." };
                }

                var cacheKey = CacheKeys.MyCountsKey(request.EmployeeId);

                var cached = await cache.GetAsync(cacheKey, ct);
                if (!string.IsNullOrWhiteSpace(cached))
                {
                    var dto = JsonSerializer.Deserialize<MyLeaveCountsInfo>(cached) ?? new MyLeaveCountsInfo();
                    return new Result(HttpStatusCode.OK) { Data = dto };
                }

                var raw = await db.LeaveRequests
                    .AsNoTracking()
                    .Where(x => x.EmployeeId == request.EmployeeId)
                    .GroupBy(x => x.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync(ct);

                var result = new MyLeaveCountsInfo
                {
                    Submitted = raw.Where(r => r.Status == LeaveStatus.Submitted).Select(r => r.Count).FirstOrDefault(),
                    Returned = raw.Where(r => r.Status == LeaveStatus.Returned).Select(r => r.Count).FirstOrDefault(),
                    Approved = raw.Where(r => r.Status == LeaveStatus.Approved).Select(r => r.Count).FirstOrDefault(),
                    Cancelled = raw.Where(r => r.Status == LeaveStatus.Cancelled).Select(r => r.Count).FirstOrDefault(),
                };

                await cache.SetAsync(
                    cacheKey,
                    JsonSerializer.Serialize(result),
                    absoluteExpiry:null,
                    tags: new[] { CacheKeys.MyListPrefix(request.EmployeeId) },
                    slidingExpiry: null,
                    cancellationToken: ct);

                return new Result(HttpStatusCode.OK) { Data = result };
            }
        }
    }
}
