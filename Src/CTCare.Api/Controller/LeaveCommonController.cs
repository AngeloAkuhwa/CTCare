using System.Net;

using CTCare.Infrastructure.Leave.Querries;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CTCare.Api.Controller
{
    [ApiController]
    [Route("api/v1/leave")]
    [Authorize]
    public class LeaveCommonController(IMediator mediator): BaseApiController
    {
        private readonly IMediator _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));

        /// <summary>
        /// Returns the list of active leave types for UI dropdowns.
        /// </summary>
        /// <response code="200">Active leave types fetched successfully.</response>
        /// <response code="401">Unauthorized (missing/invalid token).</response>
        [HttpGet("types")]
        [ProducesResponseType(typeof(GetActiveLeaveTypes.Result), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetActiveTypes(CancellationToken ct)
        {
            var result = await _mediator.Send(new GetActiveLeaveTypes.Query(), ct);
            return FromResult(result);
        }
    }
}
