using System.Net;
using System.Security.Claims;

using CTCare.Api.Extensions;
using CTCare.Infrastructure.Command;
using CTCare.Infrastructure.Utilities;
using CTCare.Shared.Settings;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CTCare.Api.Controller;

[ApiController]
[Route("api/v1/Auth")]
public class AuthenticationController(IMediator mediator, IOptions<AppSettings> appSettings, IHttpContextAccessor http): BaseApiController
{
    /// <summary>
    /// Registers a new user account
    /// </summary>
    /// <param name="command">User registration details including email, password, and personal information</param>
    /// <returns>Authentication tokens upon successful registration</returns>
    /// <response code="201">User registered successfully</response>
    /// <response code="400">Invalid registration data or user already exists</response>
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterUser.AuthResult), (int)HttpStatusCode.Created)]
    [ProducesResponseType(typeof(RegisterUser.AuthResult), (int)HttpStatusCode.BadRequest)]
    public async Task<IActionResult> RegisterAsync([FromBody] RegisterUser.Command command)
    {
        var result = await mediator.Send(command);
        return FromResult(result);
    }

    /// <summary>
    /// Authenticates a user and returns JWT + refresh token.
    /// If OTP is required, returns 202 Accepted with OtpRequired=true.
    /// </summary>
    /// <param name="command">Email &amp; password (optionally IP/UserAgent).</param>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(UserLogin.Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UserLogin.Result), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(UserLogin.Result), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(UserLogin.Result), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> LoginAsync([FromBody] UserLogin.Command command)
    {
        // Fill IP/UserAgent if not provided by client
        command.Ip ??= HttpContext.Connection.RemoteIpAddress?.ToString();
        if (string.IsNullOrWhiteSpace(command.UserAgent) &&
            Request.Headers.TryGetValue("User-Agent", out var ua))
        {
            command.UserAgent = ua.ToString();
        }

        var result = await mediator.Send(command);
        return FromResult(result);
    }

    /// <summary>
    /// Exchanges a valid refresh token for a new access token &amp; refresh token (rotation).
    /// </summary>
    /// <param name="command">
    /// The refresh token previously issued. Optionally include IP/UserAgent for audit.
    /// Set <c>RevokeAllSessions=true</c> to log out other sessions.
    /// </param>
    /// <returns>
    /// 200 with new tokens on success; 401 if the refresh token is invalid/expired; 403 if the account is not active.
    /// </returns>
    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(Refresh.Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Refresh.Result), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(Refresh.Result), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RefreshAsync([FromBody] Refresh.Command command)
    {
        // Fill IP/UserAgent if not provided by client
        command.Ip ??= HttpContext.Connection.RemoteIpAddress?.ToString();
        if (string.IsNullOrWhiteSpace(command.UserAgent) &&
            Request.Headers.TryGetValue("User-Agent", out var ua))
        {
            command.UserAgent = ua.ToString();
        }

        var result = await mediator.Send(command);
        return FromResult(result);
    }

    /// <summary>
    /// Confirms a user's email using the token sent via email.
    /// </summary>
    /// <param name="command">
    /// The confirmation token as received in the email link. Optionally include the email for extra verification.
    /// </param>
    /// <returns>
    /// 200 OK when confirmed (idempotent: returns Confirmed=true even if already confirmed),
    /// 400 Bad Request if token is invalid or expired.
    /// </returns>
    [HttpPost("confirm-email")]
    [ProducesResponseType(typeof(ConfirmEmail.Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ConfirmEmail.Result), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmEmailAsync([FromBody] ConfirmEmail.Command command)
    {
        var result = await mediator.Send(command);
        return FromResult(result);
    }

    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmailWeb([FromQuery] string token, [FromQuery] string? email)
    {
        var result = await mediator.Send(new ConfirmEmail.Command { Token = token, Email = email });

        var status = result is { Status: HttpStatusCode.OK, Confirmed: true }
            ? (result.AlreadyConfirmed ? "already" : "ok")
            : "invalid";

        var uiRoot = UrlBuilder.BuildUiRoot(appSettings, http);
        var uiUrl = UrlBuilder.WithQuery(
            UrlBuilder.Combine(uiRoot, "confirmed"),
            new Dictionary<string, string?> { ["status"] = status }
        );
        return Redirect(uiUrl);
    }


    /// <summary>
    /// Resends the email confirmation link to the specified address (if an account exists).
    /// </summary>
    /// <param name="command">The email address to send the confirmation link to.</param>
    /// <returns>
    /// Always returns 200 OK to prevent user enumeration. 
    /// If the account is already confirmed (and known), <c>AlreadyConfirmed</c> is true.
    /// </returns>
    [AllowAnonymous]
    [HttpPost("resend-confirmation")]
    [ProducesResponseType(typeof(ResendEmailConfirmation.Result), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResendConfirmationAsync([FromBody] ResendEmailConfirmation.Command command)
    {
        var result = await mediator.Send(command);
        return FromResult(result);
    }

    /// <summary>
    /// Initiates a password reset by sending a reset link to the email if an account exists.
    /// </summary>
    /// <param name="command">Email address to receive the password reset link.</param>
    /// <returns>
    /// Always 200 OK to prevent user enumeration. 
    /// <c>NotEligible</c> is true when the account is known but not eligible for reset (e.g., terminated/suspended).
    /// </returns>
    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ForgotPassword.Result), StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPasswordAsync([FromBody] ForgotPassword.Command command)
    {
        var result = await mediator.Send(command);
        return FromResult(result);
    }

    /// <summary>
    /// Resets the user's password using a valid reset token.
    /// </summary>
    /// <param name="command">
    /// The reset token (as received via email) and the new password (with confirmation).
    /// </param>
    /// <returns>
    /// 200 OK when the password is successfully changed; 400 on invalid/expired token or policy failure.
    /// </returns>
    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(ResetPassword.Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResetPassword.Result), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetAsync([FromBody] ResetPassword.Command command)
    {
        var result = await mediator.Send(command);
        return FromResult(result);
    }

    /// <summary>
    /// Landing endpoint used by the email link. Redirects to the UI reset page with token/email.
    /// </summary>
    /// <remarks>
    /// This does NOT change the password. It only moves the user to the UI with the token.
    /// </remarks>
    /// <param name="token">One-time reset token from the email link.</param>
    /// <param name="email">User email (optional in the link, if you include it).</param>
    [HttpGet("reset-password")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPasswordLanding([FromQuery] string token, [FromQuery] string? email)
    {
        var result = await mediator.Send(new ValidateResetToken.Query { Token = token, Email = email });
        if (result.Status != HttpStatusCode.OK)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Redirect(result.RedirectUrl);
    }

    [Authorize(Policy = SecurityExtensions.PolicyJwtAndApi)]
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll([FromBody] Logout.Command cmd)
    {
        var empIdClaim = User.FindFirst("empId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(empIdClaim, out var empId))
        {
            return Forbid();
        }

        cmd.EmployeeId = empId;

        await mediator.Send(cmd);

        return NoContent();
    }
}
