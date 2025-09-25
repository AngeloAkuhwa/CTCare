using System.Net;

using CTCare.Infrastructure.Leave.Querries;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CTCare.Api.Controller
{
    [ApiController]
    [Route("api/v1/leave/requests/my")]
    [Authorize(Roles = "Employee")]
    public class LeaveStatsController(IMediator mediator): BaseApiController
    {
        /// <summary>
        /// Returns aggregate counters for the current user's leave requests,
        /// grouped by status (Submitted, Returned, Approved, Cancelled).
        /// Useful for rendering dashboard badges.
        /// </summary>
        /// <response code="200">Counters successfully retrieved.</response>
        /// <response code="401">Unauthorized (missing/invalid token).</response>
        [HttpGet("counts")]
        [ProducesResponseType(typeof(GetMyLeaveCounts.Result), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetCounts(CancellationToken ct)
        {
            var employeeId = GetEmployeeIdClaim();
            if (employeeId == Guid.Empty)
            {
                return Unauthorized();
            }

            var result = await mediator.Send(new GetMyLeaveCounts.Query
            {
                EmployeeId = employeeId
            }, ct);

            return FromResult(result);
        }
    }
}
