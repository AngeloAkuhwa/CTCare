using System.Net;

using CTCare.Domain.Enums;
using CTCare.Infrastructure.Leave.Commands;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CTCare.Api.Controller
{
    [ApiController]
    [Route("api/v1/files")]
    [Authorize]
    public sealed class FilesController(IMediator mediator): BaseApiController
    {
        /// <summary>
        /// Uploads a document for a specific leave request (e.g., doctor's note).
        /// </summary>
        /// <param name="leaveRequestId">Target leave request ID.</param>
        /// <param name="kind">Document kind (e.g., DoctorsNote).</param>
        /// <param name="file">Uploaded file (multipart/form-data).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <response code="201">File uploaded and linked successfully.</response>
        /// <response code="400">Invalid input (missing file, unsupported type, etc.).</response>
        /// <response code="401">Unauthorized (missing/invalid token).</response>
        /// <response code="403">Forbidden (user not allowed to attach to this request).</response>
        /// <response code="404">Leave request not found.</response>
        /// <response code="413">Payload too large (exceeds server limits).</response>
        [HttpPost("leave/{leaveRequestId:guid}")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(UploadLeaveDocument.Result), (int)HttpStatusCode.Created)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType((int)HttpStatusCode.Forbidden)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> UploadLeaveDocument(
            [FromRoute] Guid leaveRequestId,
            [FromQuery] DocumentKind kind,
            [FromForm] UploadLeaveDocument.UploadLeaveDocumentForm form,
            CancellationToken ct)
        {
            var me = GetEmployeeIdClaim();
            if (me == Guid.Empty)
            {
                return Unauthorized();
            }

            if (form.File.Length == 0)
            {
                return BadRequest("No file provided.");
            }

            await using var stream = form.File.OpenReadStream();

            var cmd = new UploadLeaveDocument.Command
            {
                UploaderEmployeeId = me,
                LeaveRequestId = leaveRequestId,
                Kind = kind,
                Content = stream,
                FileName = form.File.FileName,
                ContentType = form.File.ContentType,
                Length = form.File.Length
            };

            var result = await mediator.Send(cmd, ct);
            return FromResult(result);
        }
    }
}
