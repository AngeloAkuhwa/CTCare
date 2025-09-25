using System.Net;
using System.Text.Json;

using CTCare.Application.Interfaces;
using CTCare.Infrastructure.Persistence;
using CTCare.Shared.BasicResult;
using CTCare.Shared.Settings;

using MediatR;

using Microsoft.EntityFrameworkCore;

namespace CTCare.Infrastructure.Leave.Querries
{
    public static class GetMyLeaveBalance
    {
        public sealed class Query: IRequest<Result>
        {
            public Guid EmployeeId { get; set; }
            public Guid? LeaveTypeId { get; set; }
            public int? Year { get; set; }  
        }

        public sealed class Result: BasicActionResult
        {
            public Result(HttpStatusCode status) : base(status) { }

            public int Year { get; set; }
            public Guid? LeaveTypeId { get; set; }
            public decimal Entitled { get; set; }
            public decimal Used { get; set; }
            public decimal Pending { get; set; }
            public decimal Available { get; set; }
        }

        public sealed class Handler(CtCareDbContext db, ICacheService cache): IRequestHandler<Query, Result>
        {

            private const string ErrorInternal = "Failed to load leave balance.";

            public async Task<Result> Handle(Query req, CancellationToken ct)
            {
                var year = req.Year ?? DateTime.UtcNow.Year;

                if (req.LeaveTypeId is null)
                {
                    var cacheKey = CacheKeys.BalanceKey(req.EmployeeId, year);
                    var cached = await cache.GetAsync(cacheKey, ct);
                    if (!string.IsNullOrWhiteSpace(cached))
                    {
                        try
                        {
                            var parsed = JsonSerializer.Deserialize<Result>(cached);
                            if (parsed is not null)
                            {
                                return parsed;
                            }
                        }
                        catch
                        {
                            // If deserialization fails, fall through to DB fetch
                        }
                    }
                }

                try
                {
                    var query = db.LeaveBalances.AsNoTracking()
                        .Where(x => x.EmployeeId == req.EmployeeId && x.Year == year);

                    if (req.LeaveTypeId.HasValue)
                    {
                        query = query.Where(x => x.LeaveTypeId == req.LeaveTypeId.Value);
                    }

                    var agg = await query
                        .GroupBy(_ => 1)
                        .Select(g => new
                        {
                            Entitled = g.Sum(b => b.EntitledDays),
                            Used = g.Sum(b => b.UsedDays),
                            Pending = g.Sum(b => b.PendingDays)
                        })
                        .FirstOrDefaultAsync(ct);

                    var entitled = agg?.Entitled ?? 0m;
                    var used = agg?.Used ?? 0m;
                    var pending = agg?.Pending ?? 0m;
                    var available = entitled - used - pending;

                    var result = new Result(HttpStatusCode.OK)
                    {
                        Year = year,
                        LeaveTypeId = req.LeaveTypeId,
                        Entitled = entitled,
                        Used = used,
                        Pending = pending,
                        Available = available
                    };

                    // Write aggregate snapshot to cache
                    if (req.LeaveTypeId is not null)
                    {
                        return result;
                    }

                    var cacheKey = CacheKeys.BalanceKey(req.EmployeeId, year);
                    var json = JsonSerializer.Serialize(result);
                    await cache.SetAsync(
                        cacheKey,
                        json,
                        absoluteExpiry: null,
                        slidingExpiry: null,
                        token: ct
                    );

                    return result;
                }
                catch
                {
                    return new Result(HttpStatusCode.InternalServerError)
                    {
                        ErrorMessage = ErrorInternal
                    };
                }
            }
        }
    }
}
