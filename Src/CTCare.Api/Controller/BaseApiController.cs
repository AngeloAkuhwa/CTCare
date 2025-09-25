using System.Net;
using System.Security.Claims;

using CTCare.Shared.BasicResult;

using Microsoft.AspNetCore.Mvc;

using IActionResult = Microsoft.AspNetCore.Mvc.IActionResult;

namespace CTCare.Api.Controller
{
    [ApiController]
    public abstract class BaseApiController: ControllerBase
    {
        /// <summary>
        /// Converts a BasicActionResult (or subclass) into an IActionResult
        /// using its StatusCode and payload.
        /// </summary>
        protected IActionResult FromResult<T>(T result) where T : BasicActionResult
        {
            return result.Status switch
            {
                HttpStatusCode.OK => Ok(result),
                HttpStatusCode.Created => StatusCode(StatusCodes.Status201Created, result),
                HttpStatusCode.Accepted => StatusCode(StatusCodes.Status202Accepted, result),
                HttpStatusCode.NoContent => NoContent(),
                HttpStatusCode.BadRequest => BadRequest(result),
                HttpStatusCode.Unauthorized => Unauthorized(result),
                HttpStatusCode.Forbidden => StatusCode(StatusCodes.Status403Forbidden, result),
                HttpStatusCode.NotFound => NotFound(result),
                _ => StatusCode((int)result.Status, result)
            };
        }

        protected Guid GetEmployeeIdClaim()
        {
            var raw = User.FindFirstValue("employee_id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
        }
    }
}
