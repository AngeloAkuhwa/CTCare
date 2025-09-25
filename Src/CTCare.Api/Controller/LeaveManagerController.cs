using CTCare.Application.Leaves;
using CTCare.Infrastructure.Leave.Commands;
using CTCare.Infrastructure.Leave.Querries;
using CTCare.Shared.Interfaces;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CTCare.Api.Controller
{
    /// <summary>
    /// Manager-facing endpoints for reviewing, approving, returning, and cancelling team leave requests.
    /// </summary>
    /// <remarks>
    /// All endpoints require an authenticated user with the <c>Manager</c> policy.
    /// Manager authorization is re-validated per request using the authenticated manager's EmployeeId claim.
    /// </remarks>
    [ApiController]
    [Route("api/v1/leave/manager")]
    [Authorize(Policy = "EngineeringManager")]
    public class LeaveManagerController(IMediator mediator): BaseApiController
    {
        private readonly IMediator _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));

        /// <summary>
        /// Get a paged list of your team's leave requests (filterable by status/date).
        /// </summary>
        /// <param name="filter">Status CSV, date range, paging, and page size.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Paged list of team leave requests visible to the current manager.</returns>
        /// <response code="200">Returns the paged list.</response>
        /// <response code="401">User is not authenticated or missing an employee id.</response>
        [HttpGet("requests/team")]
        [ProducesResponseType(typeof(PagedResult<TeamLeaveItemInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetTeamAsync([FromQuery] TeamLeaveFilterRequest filter, CancellationToken ct)
        {
            var managerId = GetEmployeeIdClaim();
            if (managerId == Guid.Empty)
            {
                return Unauthorized();
            }

            var query = new GetTeamLeaveRequests.Query
            {
                ManagerId = managerId,
                Filter = filter ?? new TeamLeaveFilterRequest()
            };

            var page = await _mediator.Send(query, ct);
            return Ok(page);
        }

        /// <summary>
        /// Approve a submitted leave request that belongs to your team.
        /// </summary>
        /// <param name="id">The leave request ID.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>No content on success.</returns>
        /// <response code="204">Request approved.</response>
        /// <response code="400">Invalid state or validation error.</response>
        /// <response code="401">User is not authenticated.</response>
        /// <response code="403">User is not the manager for this request.</response>
        /// <response code="404">Leave request not found.</response>
        [HttpPost("requests/{id:guid}/approve")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ApproveAsync([FromRoute] Guid id, CancellationToken ct)
        {
            var managerId = GetEmployeeIdClaim();
            if (managerId == Guid.Empty)
            {
                return Unauthorized();
            }

            var result = await _mediator.Send(new ApproveLeave.Command
            {
                ManagerId = managerId,
                LeaveRequestId = id
            }, ct);

            return FromResult(result);
        }

        /// <summary>
        /// Return a submitted leave request to the employee for correction (no hard reject).
        /// </summary>
        /// <param name="id">The leave request ID.</param>
        /// <param name="command">Manager comment (required).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>No content on success.</returns>
        /// <response code="204">Request returned for correction.</response>
        /// <response code="400">Invalid state or comment missing.</response>
        /// <response code="401">User is not authenticated.</response>
        /// <response code="403">User is not the manager for this request.</response>
        /// <response code="404">Leave request not found.</response>
        [HttpPost("requests/{id:guid}/return")]
        [Consumes("application/json")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ReturnForCorrectionAsync(
            [FromRoute] Guid id,
            [FromBody] ReturnLeaveForCorrection.Command command,
            CancellationToken ct)
        {
            var managerId = GetEmployeeIdClaim();
            if (managerId == Guid.Empty)
            {
                return Unauthorized();
            }

            command.LeaveRequestId = id;
            command.ManagerId = managerId;

            var result = await _mediator.Send(command, ct);
            return FromResult(result);
        }

        /// <summary>
        /// Get detailed information for a specific team leave request,
        /// including audit trail (events) and doctor’s note link (if any).
        /// </summary>
        /// <param name="id">The leave request ID.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Detailed request data if the caller is the manager.</returns>
        /// <response code="200">Returns the detailed request data.</response>
        /// <response code="401">User is not authenticated.</response>
        /// <response code="403">User is not the manager for this request.</response>
        /// <response code="404">Leave request not found.</response>
        [HttpGet("requests/{id:guid}")]
        [ProducesResponseType(typeof(GetTeamLeaveRequestDetails.TeamDetailsInfo), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetDetails([FromRoute] Guid id, CancellationToken ct)
        {
            var managerId = GetEmployeeIdClaim();
            if (managerId == Guid.Empty)
            {
                return Unauthorized();
            }

            var query = new GetTeamLeaveRequestDetails.Query
            {
                ManagerId = managerId,
                LeaveRequestId = id
            };

            var result = await _mediator.Send(query, ct);
            return FromResult(result);
        }

        /// <summary>
        /// Cancel an employee’s <c>Submitted</c> or <c>Returned</c> leave request as manager.
        /// </summary>
        /// <param name="id">The leave request ID.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>No content on success.</returns>
        /// <response code="204">Request cancelled.</response>
        /// <response code="400">Invalid state to cancel.</response>
        /// <response code="401">User is not authenticated.</response>
        /// <response code="403">User is not the manager for this request.</response>
        /// <response code="404">Leave request not found.</response>
        [HttpPost("requests/{id:guid}/cancel-by-manager")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CancelByManager([FromRoute] Guid id, CancellationToken ct)
        {
            var managerId = GetEmployeeIdClaim();
            if (managerId == Guid.Empty)
            {
                return Unauthorized();
            }

            var cmd = new CancelLeaveByManager.Command
            {
                ManagerId = managerId,
                LeaveRequestId = id
            };

            var result = await _mediator.Send(cmd, ct);
            return FromResult(result);
        }
    }
}
