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
    public static class GetActiveLeaveTypes
    {
        public sealed class Query: IRequest<Result> { }

        public sealed class Result: BasicActionResult
        {
            public Result(HttpStatusCode status) : base(status) { }
            public Result(string error) : base(error) { }
            public IReadOnlyList<LeaveTypeInfo> Items { get; init; } = Array.Empty<LeaveTypeInfo>();
        }

        public sealed class LeaveTypeInfo
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
        }

        public sealed class Handler(CtCareDbContext db, ICacheService cache): IRequestHandler<Query, Result>
        {
            public async Task<Result> Handle(Query request, CancellationToken ct)
            {
                var cacheKey = CacheKeys.ActiveLeaveTypes;
                var cached = await cache.GetAsync(cacheKey, ct);
                if (!string.IsNullOrWhiteSpace(cached))
                {
                    var fromCache = JsonSerializer.Deserialize<List<LeaveTypeInfo>>(cached)
                                   ?? new List<LeaveTypeInfo>();
                    return new Result(HttpStatusCode.OK) { Items = fromCache };
                }

                var items = await db.LeaveTypes
                    .AsNoTracking()
                    .Where(t => !t.IsDeleted) 
                    .OrderBy(t => t.Name)
                    .Select(t => new LeaveTypeInfo
                    {
                        Id = t.Id,
                        Name = t.Name
                    })
                    .ToListAsync(ct);

                var payload = JsonSerializer.Serialize(items);
                await cache.SetAsync(
                    cacheKey,
                    payload,
                    absoluteExpiry: null,
                    slidingExpiry: null,
                    token: ct);

                return new Result(HttpStatusCode.OK) { Items = items };
            }
        }
    }
}
