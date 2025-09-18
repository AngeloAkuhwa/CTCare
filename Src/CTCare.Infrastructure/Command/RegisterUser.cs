using System.ComponentModel.DataAnnotations;
using System.Net;

using CTCare.Application.Notification;
using CTCare.Domain.Entities;
using CTCare.Domain.Enums;
using CTCare.Infrastructure.Persistence;
using CTCare.Infrastructure.Security;
using CTCare.Infrastructure.Utilities;
using CTCare.Shared.BasicResult;
using CTCare.Shared.Settings;
using CTCare.Shared.Utilities;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CTCare.Infrastructure.Command;

public static class RegisterUser
{
    public class Command: IRequest<AuthResult>
    {
        public PersonalInfo Personal { get; set; }
        public ContactInfo Contact { get; set; }
        public EmploymentInfo Employment { get; set; }
    }

    public class PersonalInfo
    {
        [Required, StringLength(100)] public string FirstName { get; set; }
        [Required, StringLength(100)] public string LastName { get; set; }
        [Required] public DateTime? DateOfBirth { get; set; }
        [Required] public Gender? Sex { get; set; }
    }

    public class ContactInfo
    {
        [Required, EmailAddress] public string Email { get; set; }
        [Required, MinLength(8)] public string Password { get; set; }
    }

    public class EmploymentInfo
    {
        [Required] public Guid DepartmentId { get; set; }
        public Guid? TeamId { get; set; }
        [Required, StringLength(200)] public string Designation { get; set; }
        public Guid? ManagerEmployeeId { get; set; }
    }

    public class AuthResult: BasicActionResult
    {
        public AuthResult(HttpStatusCode status) : base(status) { }
        public AuthResult(string error) : base(error) { }

        public Guid EmployeeId { get; init; }
        public string Email { get; init; } = "";
        public bool EmailSent { get; init; }
    }

    public class Handler(
        CtCareDbContext db,
        IPasswordHasher hasher,
        IEmailService email,
        IOptions<AppSettings> appSettingsCfg,
        IOptions<LeaveRulesSettings> leaveRulesCfg,
        IOptions<AuthSettings> authCfg,
        ILogger<Handler> log,
        IRefreshTokenService refreshToken,
        IHttpContextAccessor http
    ): IRequestHandler<Command, AuthResult>
    {
        private const string ConfirmEmailTemplate = "Templates.Email.ConfirmAccount.cshtml";
        private const string EmailSubject = "Confirm your CTCare account";
        private const string EmailExistsMessage = "Email already exists.";
        private const string InvalidDepartment = "Invalid Department.";
        private const string InvalidTeam = "Invalid Team or Team not under Department";
        private const string InvalidManager = "Invalid Manager Info";
        private const string InternalError = "Registration failed for {0}";

        public async Task<AuthResult> Handle(Command req, CancellationToken ct)
        {
            var emailNorm = (req.Contact.Email ?? string.Empty).Trim().ToLowerInvariant();

            using var scope = log.BeginScope(new Dictionary<string, object>
            {
                ["op"] = "register_user",
                ["email"] = emailNorm
            });

            if (req.Personal.DateOfBirth is null || req.Personal.Sex is null)
            {
                return new AuthResult(HttpStatusCode.BadRequest) { ErrorMessage = "Date of birth and Sex are required." };
            }

            // Ensure email uniqueness by normalized value
            var emailExists = await db.UserAccounts.AsNoTracking().AnyAsync(u => u.Email == emailNorm, ct);
            if (emailExists)
            {
                return new AuthResult(EmailExistsMessage);
            }

            var dept = await db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == req.Employment.DepartmentId, ct);
            if (dept is null)
            {
                return new AuthResult(InvalidDepartment);
            }

            if (req.Employment.TeamId is { } teamId)
            {
                var teamOk = await db.Teams.AsNoTracking().AnyAsync(t => t.Id == teamId && t.DepartmentId == dept.Id, ct);
                if (!teamOk)
                {
                    return new AuthResult(InvalidTeam);
                }
            }

            if (req.Employment.ManagerEmployeeId is { } mgrId)
            {
                var mgrOk = await db.Employees.AsNoTracking().AnyAsync(e => e.Id == mgrId, ct);
                if (!mgrOk)
                {
                    return new AuthResult(InvalidManager);
                }
            }

            var employeeCode = await EmployeeIdGenerator.GenerateAsync(db);

            var employee = new Employee
            {
                Id = SequentialGuid.NewGuid(),
                EmployeeCode = employeeCode,
                FirstName = req.Personal.FirstName,
                LastName = req.Personal.LastName,
                DateOfBirth = req.Personal.DateOfBirth.Value,
                Sex = req.Personal.Sex.Value,

                Email = emailNorm,
                EmailStatus = EmailStatus.Pending,

                DepartmentId = req.Employment.DepartmentId,
                TeamId = req.Employment.TeamId,
                Designation = req.Employment.Designation,
                ManagerId = req.Employment.ManagerEmployeeId,

                Status = EmploymentStatus.Pending,
                DateOfHire = DateTimeOffset.UtcNow,

                AnnualLeaveDays = leaveRulesCfg.Value.AnnualLeaveDays,
                SickLeaveDays = leaveRulesCfg.Value.SickLeaveDays,
                AnnualLeaveBalance = leaveRulesCfg.Value.AnnualLeaveBalance,
                SickLeaveBalance = leaveRulesCfg.Value.SickLeaveBalance
            };

            var (hash, salt) = hasher.Hash(req.Contact.Password);
            var account = new User
            {
                Id = SequentialGuid.NewGuid(),
                EmployeeId = employee.Id,
                Email = emailNorm,
                PasswordHash = hash,
                PasswordSalt = salt,
                EmailConfirmed = false,
                TwoFactorEnabled = false
            };

            var confirmTtl = authCfg.Value.PasswordResetTokenExpiryMinutes;

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                db.Employees.Add(employee);
                db.UserAccounts.Add(account);

                var confirmToken = refreshToken.GenerateUrlSafeToken();
                db.RefreshTokens.Add(new RefreshToken
                {
                    Id = SequentialGuid.NewGuid(),
                    EmployeeId = employee.Id,
                    Token = $"confirm:{confirmToken}",
                    ExpiresAt = DateTimeOffset.UtcNow.Add(confirmTtl),
                    Revoked = false,
                    CreatedBy = account.Id
                });

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                var authBase = UrlBuilder.BuildAuthBase(http, appSettingsCfg);
                var confirmUrl = UrlBuilder.WithQuery(
                    UrlBuilder.Combine(authBase, "confirm-email"),
                    new Dictionary<string, string?>
                    {
                        ["token"] = confirmToken,
                        ["email"] = emailNorm
                    });

                bool emailSent;
                try
                {
                    var html = await email.RenderTemplateAsync(ConfirmEmailTemplate, new
                    {
                        Name = $"{employee.FirstName} {employee.LastName}",
                        Url = confirmUrl
                    });

                    await email.SendEmailAsync(emailNorm, EmailSubject, html, ct: ct);
                    emailSent = true;
                }
                catch (Exception mailEx)
                {
                    // Don't fail the registration if email sending fails
                    emailSent = false;
                    log.LogWarning(mailEx, "User created but failed to send confirmation email to {Email}", emailNorm);
                }

                return new AuthResult(HttpStatusCode.Created)
                {
                    EmployeeId = employee.Id,
                    Email = employee.Email,
                    EmailSent = emailSent
                };
            }
            catch (DbUpdateException dupEx)
            {
                await tx.RollbackAsync(ct);
                log.LogWarning(dupEx, "Registration duplicate for {Email}", emailNorm);

                return new AuthResult(EmailExistsMessage);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                log.LogError(ex, string.Format(InternalError, emailNorm));

                return new AuthResult(HttpStatusCode.InternalServerError)
                {
                    ErrorMessage = "An internal error occurred while creating the account."
                };
            }
        }
    }
}
