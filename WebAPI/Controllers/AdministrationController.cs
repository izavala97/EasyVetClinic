using System.Data;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EasyVetClinic.Api.Authentication;
using EasyVetClinic.Api.Data;
using EasyVetClinic.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyVetClinic.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class AdministrationController(ClinicDbContext dbContext, CurrentClinic currentClinic,
    ILogger<AdministrationController> logger) : ControllerBase
{
    private static readonly string[] AssignableRoles = [ClinicRoles.ClinicAdmin, ClinicRoles.Veterinarian, ClinicRoles.Staff];

    [HttpGet("platform/clinics")]
    [RequirePlatformOwner]
    public async Task<ActionResult<IReadOnlyList<PlatformClinicSummary>>> ListClinics() =>
        Ok(await dbContext.Clinics.AsNoTracking().OrderBy(clinic => clinic.Name)
            .Select(clinic => new PlatformClinicSummary(clinic.Id, clinic.Name, clinic.IsSuspended,
                clinic.SuspensionReason, clinic.Users.Count(user => user.IsActive))).ToListAsync());

    [HttpPut("platform/clinics/{clinicId}/access")]
    [RequirePlatformOwner]
    public async Task<IActionResult> UpdateAccess(string clinicId, UpdateClinicAccessRequest request)
    {
        if (request.Reason.Length > 500 || (request.IsSuspended && string.IsNullOrWhiteSpace(request.Reason)))
            return ValidationProblem("A suspension reason of up to 500 characters is required.");
        var clinic = await dbContext.Clinics.SingleOrDefaultAsync(clinic => clinic.Id == clinicId);
        if (clinic is null) return NotFound();
        clinic.IsSuspended = request.IsSuspended;
        clinic.SuspensionReason = request.IsSuspended ? request.Reason.Trim() : "";
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Platform owner {ActorId} changed clinic {ClinicId} suspension to {IsSuspended}",
            User.FindFirstValue("oid"), clinicId, request.IsSuspended);
        return NoContent();
    }

    [HttpGet("clinic/users")]
    [RequireClinicRole(ClinicRoles.ClinicAdmin)]
    public async Task<ActionResult<IReadOnlyList<ClinicUserSummary>>> ListUsers()
    {
        var clinicId = currentClinic.Id;
        return Ok(await dbContext.ClinicUsers.AsNoTracking().Where(user => user.ClinicId == clinicId)
            .OrderBy(user => user.DisplayName)
            .Select(user => new ClinicUserSummary(user.Id, user.DisplayName, user.Role, user.IsActive)).ToListAsync());
    }

    [HttpPut("clinic/users/{userId}")]
    [RequireClinicRole(ClinicRoles.ClinicAdmin)]
    public async Task<IActionResult> UpdateUser(string userId, UpdateClinicUserRequest request)
    {
        if (!AssignableRoles.Contains(request.Role)) return ValidationProblem("Choose a valid clinic role.");
        var clinicId = currentClinic.Id;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var user = await dbContext.ClinicUsers.SingleOrDefaultAsync(user => user.ClinicId == clinicId && user.Id == userId);
        if (user is null) return NotFound();
        if (user.Role == ClinicRoles.SuperAdmin) return Forbid();
        if (user.IsActive && user.Role == ClinicRoles.ClinicAdmin && (!request.IsActive || request.Role != ClinicRoles.ClinicAdmin)
            && !await dbContext.ClinicUsers.AnyAsync(other => other.ClinicId == clinicId && other.Id != userId
                && other.IsActive && other.Role == ClinicRoles.ClinicAdmin))
            return Conflict(new { error = "A clinic must retain at least one active clinic administrator." });
        user.Role = request.Role;
        user.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
        logger.LogInformation("Administrator {ActorId} updated user {UserId} in clinic {ClinicId}",
            User.FindFirstValue("oid"), userId, clinicId);
        return NoContent();
    }

    [HttpGet("clinic/invitations")]
    [RequireClinicRole(ClinicRoles.ClinicAdmin)]
    public async Task<ActionResult<IReadOnlyList<InvitationSummary>>> ListInvitations()
    {
        var clinicId = currentClinic.Id;
        return Ok(await dbContext.ClinicInvitations.AsNoTracking().Where(invitation => invitation.ClinicId == clinicId)
            .Select(invitation => new InvitationSummary(invitation.Id, invitation.Email, invitation.Role,
                invitation.ExpiresAt, invitation.IsUsed, invitation.IsRevoked)).ToListAsync());
    }

    [HttpPost("clinic/invitations")]
    [RequireClinicRole(ClinicRoles.ClinicAdmin)]
    public async Task<ActionResult<CreatedInvitation>> Invite(CreateInvitationRequest request)
    {
        if (!AssignableRoles.Contains(request.Role) || request.Email.Length > 254
            || !MailAddress.TryCreate(request.Email.Trim(), out var email) || email.Address != request.Email.Trim())
            return ValidationProblem("A valid email address and clinic role are required.");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var invitation = new ClinicInvitation
        {
            Id = Guid.NewGuid().ToString(), ClinicId = currentClinic.Id, Email = email.Address,
            Role = request.Role, TokenHash = Hash(token), ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        };
        dbContext.ClinicInvitations.Add(invitation);
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Administrator {ActorId} created invitation {InvitationId} for clinic {ClinicId}",
            User.FindFirstValue("oid"), invitation.Id, invitation.ClinicId);
        return Ok(new CreatedInvitation(new InvitationSummary(invitation.Id, invitation.Email, invitation.Role,
            invitation.ExpiresAt, false, false), token));
    }

    [HttpDelete("clinic/invitations/{invitationId}")]
    [RequireClinicRole(ClinicRoles.ClinicAdmin)]
    public async Task<IActionResult> Revoke(string invitationId)
    {
        var clinicId = currentClinic.Id;
        var invitation = await dbContext.ClinicInvitations.SingleOrDefaultAsync(invitation =>
            invitation.ClinicId == clinicId && invitation.Id == invitationId);
        if (invitation is null) return NotFound();
        invitation.IsRevoked = true;
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Administrator {ActorId} revoked invitation {InvitationId}", User.FindFirstValue("oid"), invitationId);
        return NoContent();
    }

    [HttpPost("invitations/accept")]
    public async Task<ActionResult<ClinicMembershipSummary>> Accept(AcceptInvitationRequest request)
    {
        var objectId = User.FindFirstValue("oid");
        if (string.IsNullOrWhiteSpace(objectId)) return Forbid();
        if (request.Token.Length != 64) return ValidationProblem("Invalid invitation token.");
        var hash = Hash(request.Token);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var invitation = await dbContext.ClinicInvitations.Include(invitation => invitation.Clinic)
            .SingleOrDefaultAsync(invitation => invitation.TokenHash == hash);
        if (invitation is null || invitation.IsUsed || invitation.IsRevoked || invitation.ExpiresAt <= DateTimeOffset.UtcNow)
            return BadRequest(new { error = "This invitation is invalid, expired, revoked, or already accepted." });
        if (!PlatformAccess.HasEmail(User, invitation.Email))
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Sign in with the invited email address. The API token must include your verified email." });
        if (invitation.Clinic.IsSuspended)
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "This clinic is suspended. Contact the platform owner." });
        if (await dbContext.ClinicUsers.AnyAsync(user => user.ClinicId == invitation.ClinicId && user.EntraObjectId == objectId))
            return Conflict(new { error = "You already have a membership in this clinic. Ask its administrator to update your access." });
        // Conditional consumption keeps a link single-use even with concurrent acceptance/revocation.
        var consumed = await dbContext.ClinicInvitations.Where(candidate =>
            candidate.Id == invitation.Id && !candidate.IsUsed && !candidate.IsRevoked)
            .ExecuteUpdateAsync(setters => setters.SetProperty(candidate => candidate.IsUsed, true));
        if (consumed != 1) return Conflict(new { error = "This invitation is no longer available." });
        dbContext.ClinicUsers.Add(new ClinicUser
        {
            Id = Guid.NewGuid().ToString(), ClinicId = invitation.ClinicId, EntraObjectId = objectId,
            DisplayName = User.Identity?.Name ?? invitation.Email, Role = invitation.Role
        });
        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
        logger.LogInformation("User {ActorId} accepted invitation {InvitationId} for clinic {ClinicId}", objectId, invitation.Id, invitation.ClinicId);
        return Ok(new ClinicMembershipSummary(invitation.ClinicId, invitation.Clinic.Name, invitation.Role, false));
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
