using System.Net;

using CTCare.Infrastructure.Leave.Commands;
using CTCare.Infrastructure.Leave.Querries;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CTCare.Api.Controller
{
    [Authorize(Roles = "Employee")]
    [ApiController]
    [Produces("application/json")]
    [Route("api/v1/leave")]
    public class LeaveEmployeeController(IMediator mediator): BaseApiController
    {
        /// <summary>
        /// Create & submit a new leave request for the current user.
        /// Validates business rules (business days, half-day, doc rule, overlap),
        /// reserves pending balance, persists request + audit "Submitted".
        /// </summary>
        [HttpPost("requests")]
        [ProducesResponseType(typeof(SubmitLeave.Result), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> SubmitAsync([FromBody] SubmitLeave.Command body)
        {
            var me = GetEmployeeIdClaim();
            if (me == Guid.Empty)
            {
                return Unauthorized();
            }

            body.EmployeeId = me;
            var result = await mediator.Send(body);
            return FromResult(result);
        }

        /// <summary>
        /// Cancel one of your leave requests (if not yet approved).
        /// If the request is still submitted, this releases your pending reservation.
        /// </summary>
        [HttpPost("requests/{id:guid}/cancel")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CancelAsync([FromRoute] Guid id)
        {
            var me = GetEmployeeIdClaim();
            if (me == Guid.Empty)
            {
                return Unauthorized();
            }

            var cmd = new CancelLeave.Command
            {
                EmployeeId = me,
                LeaveRequestId = id
            };

            var ok = await mediator.Send(cmd);
            return ok ? NoContent() : BadRequest();
        }

        /// <summary>
        /// Get a snapshot of your current-year leave balance.
        /// Optionally filter to a specific LeaveType (otherwise totals are aggregated).
        /// </summary>
        [HttpGet("balance/my")]
        [ProducesResponseType(typeof(GetMyLeaveBalance.Result), (int)HttpStatusCode.OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMyBalanceAsync([FromQuery] Guid? leaveTypeId, [FromQuery] int? year)
        {
            var employeeId = GetEmployeeIdClaim();
            if (employeeId == Guid.Empty)
            {
                return Unauthorized();
            }

            var res = await mediator.Send(new GetMyLeaveBalance.Query
            {
                EmployeeId = employeeId,
                LeaveTypeId = leaveTypeId,
                Year = year
            });

            return FromResult(res);
        }

        /// <summary>
        /// Get details for a single *own* leave request, including audit trail and any doctor’s note link.
        /// Ownership/visibility is enforced in the handler.
        /// </summary>
        [HttpGet("requests/{id:guid}")]
        [ProducesResponseType(typeof(GetMyLeaveRequestDetails.Result), (int)HttpStatusCode.OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMyRequestByIdAsync([FromRoute] Guid id)
        {
            var employeeId = GetEmployeeIdClaim();
            if (employeeId == Guid.Empty)
            {
                return Unauthorized();
            }

            var res = await mediator.Send(new GetMyLeaveRequestDetails.Query
            {
                EmployeeId = employeeId,
                LeaveRequestId = id
            });

            return FromResult(res);
        }

        /// <summary>
        /// Edit a previously returned-for-correction leave request (your own).
        /// The handler should enforce the "only when Returned" rule and allowed fields.
        /// </summary>
        [HttpPut("requests/{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> EditRequestByIdAsync([FromRoute] Guid id, [FromBody] EditLeave.Command body)
        {
            var employeeId = GetEmployeeIdClaim();
            if (employeeId == Guid.Empty)
            {
                return Unauthorized();
            }

            body.EmployeeId = employeeId;
            body.LeaveRequestId = id;

            var res = await mediator.Send(body);
            return FromResult(res);
        }

        /// <summary>
        /// Re-submit a returned leave request after making corrections.
        /// Re-validates rules, re-reserves pending balance, sets status to Submitted, adds audit event.
        /// </summary>
        [HttpPost("requests/{id:guid}/resubmit")]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Resubmit([FromRoute] Guid id, [FromBody] ResubmitLeave.Command cmd)
        {
            var employeeId = GetEmployeeIdClaim();
            if (employeeId == Guid.Empty)
            {
                return Unauthorized();
            }

            cmd.EmployeeId = employeeId;
            cmd.LeaveRequestId = id;

            var result = await mediator.Send(cmd);
            return FromResult(result);
        }
    }
}
