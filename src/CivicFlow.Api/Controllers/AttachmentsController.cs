using System.Data;
using CivicFlow.Api.Common;
using CivicFlow.Application.Storage;
using CivicFlow.Domain.Entities;
using CivicFlow.Domain.Enums;
using CivicFlow.Infrastructure.Identity;
using CivicFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CivicFlow.Api.Controllers;

[ApiController, Authorize(Roles = CivicFlowRoles.Resident + "," + CivicFlowRoles.CaseOfficer + "," + CivicFlowRoles.TeamManager + "," + CivicFlowRoles.SystemAdministrator), Route("api/cases/{caseId:guid}/attachments")]
public sealed class AttachmentsController(ApplicationDbContext db, IFileStorage storage, ILogger<AttachmentsController> logger) : ControllerBase
{
    private static readonly ServiceRequestStatus[] ResidentEditableStatuses =
    [ServiceRequestStatus.Submitted, ServiceRequestStatus.Triaged, ServiceRequestStatus.Assigned, ServiceRequestStatus.InProgress, ServiceRequestStatus.WaitingForResident, ServiceRequestStatus.Reopened];

    [HttpGet]
    public async Task<IActionResult> List(Guid caseId)
    {
        var item = await AccessibleCase(caseId); if (item is null) return NotFound();
        var resident = User.IsInRole(CivicFlowRoles.Resident);
        var rows = await db.CaseAttachments.AsNoTracking().Where(x => x.ServiceRequestId == caseId && !x.IsDeleted && (!resident || x.Visibility == AttachmentVisibility.Public))
            .OrderByDescending(x => x.UploadedAtUtc).ToListAsync();
        return Ok(rows.Select(x => ToDto(x, CanDelete(item, x))));
    }

    [HttpPost, RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> Upload(Guid caseId, [FromForm] IFormFile file, [FromForm] AttachmentVisibility visibility = AttachmentVisibility.Public)
    {
        var item = await AccessibleCase(caseId); if (item is null) return NotFound();
        var resident = User.IsInRole(CivicFlowRoles.Resident);
        if (resident && (visibility != AttachmentVisibility.Public || !ResidentEditableStatuses.Contains(item.Status))) return NotFound();
        if (User.IsInRole(CivicFlowRoles.CaseOfficer) && !ResidentEditableStatuses.Contains(item.Status)) return NotFound();
        var idempotency = Request.Headers["Idempotency-Key"].ToString();
        if (idempotency.Length > 100) return BadRequest(new { message = "Idempotency key is too long." });
        var operationKey = string.IsNullOrWhiteSpace(idempotency) ? null : $"attachment:{caseId}:{idempotency}";
        if (operationKey is not null && await db.CaseActivities.AnyAsync(x => x.ActorId == User.UserId() && x.OperationKey == operationKey)) return NoContent();
        if (await db.CaseAttachments.CountAsync(x => x.ServiceRequestId == caseId && !x.IsDeleted) >= 5)
            return Conflict(new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = "Attachment limit reached",
                Detail = "A case can have at most five active attachments." });

        ValidatedAttachment validated;
        try { validated = await AttachmentFileValidator.ValidateAsync(file, HttpContext.RequestAborted); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        await using var content = validated.Content;
        var attachmentId = Guid.NewGuid(); var storageKey = $"cases/{caseId:N}/{attachmentId:N}";
        try
        {
            await storage.StoreAsync(storageKey, content, validated.ContentType,
                new Dictionary<string, string> { ["caseid"] = caseId.ToString("N"), ["attachmentid"] = attachmentId.ToString("N") }, HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Attachment storage failed for case {CaseId}; compensating the randomized object key.", caseId);
            await CompensateBlobAsync(storageKey);
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Attachment storage unavailable",
                detail: "The attachment could not be stored. Please retry.");
        }
        try
        {
            var isolation = db.Database.IsSqlServer() ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable;
            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(isolation) : null;
            if (db.Database.IsSqlServer())
            {
                var lockResource = $"CivicFlow.AttachmentUpload:{caseId:N}";
                await db.Database.ExecuteSqlInterpolatedAsync($$"""
                    DECLARE @lockResult int;
                    EXEC @lockResult = sp_getapplock
                        @Resource={{lockResource}}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;
                    IF @lockResult < 0 THROW 51000, 'Unable to acquire the case attachment lock.', 1;
                    """);
            }
            if (await db.CaseAttachments.CountAsync(x => x.ServiceRequestId == caseId && !x.IsDeleted) >= 5)
            {
                if (transaction is not null) await transaction.RollbackAsync();
                var compensated = await CompensateBlobAsync(storageKey);
                if (!compensated)
                    return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Attachment cleanup pending",
                        detail: "The attachment was not accepted and cleanup will be retried.");
                return Conflict(new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = "Attachment limit reached",
                    Detail = "A case can have at most five active attachments." });
            }
            var now = DateTimeOffset.UtcNow;
            var attachment = CaseAttachment.Create(attachmentId, caseId, User.UserId(), validated.FileName, storageKey, validated.ContentType, validated.SizeBytes, validated.Sha256, visibility, now);
            db.CaseAttachments.Add(attachment);
            var activity = new CaseActivity(caseId, User.UserId(), "AttachmentUploaded", $"Attachment uploaded: {validated.FileName} ({visibility}).", visibility == AttachmentVisibility.Public, now, operationKey);
            db.CaseActivities.Add(activity);
            NotifyUpload(item, attachment, activity);
            await db.SaveChangesAsync(); if (transaction is not null) await transaction.CommitAsync();
            return CreatedAtAction(nameof(List), new { caseId }, ToDto(attachment, CanDelete(item, attachment)));
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Attachment database write failed for case {CaseId}; the blob will be compensated.", caseId);
            await CompensateBlobAsync(storageKey);
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Attachment could not be saved",
                detail: "The attachment was not saved. Please retry.");
        }
        catch (SqlException ex)
        {
            logger.LogError(ex, "Attachment SQL operation failed for case {CaseId}; the blob will be compensated.", caseId);
            await CompensateBlobAsync(storageKey);
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Attachment service temporarily unavailable",
                detail: "The attachment was not saved. Please retry.");
        }
        catch
        {
            await CompensateBlobAsync(storageKey);
            throw;
        }
    }

    [HttpGet("{attachmentId:guid}/content")]
    public async Task<IActionResult> Download(Guid caseId, Guid attachmentId)
    {
        if (await AccessibleCase(caseId) is null) return NotFound();
        var attachment = await db.CaseAttachments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == attachmentId && x.ServiceRequestId == caseId && !x.IsDeleted);
        if (attachment is null || (User.IsInRole(CivicFlowRoles.Resident) && attachment.Visibility == AttachmentVisibility.Internal)) return NotFound();
        var stored = await storage.OpenReadAsync(attachment.StorageKey, HttpContext.RequestAborted); if (stored is null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff"; Response.Headers.ETag = $"\"sha256-{attachment.Sha256}\""; Response.ContentLength = stored.Length;
        return File(stored.Content, attachment.ContentType, AttachmentFileValidator.SafeFileName(attachment.OriginalFileName), enableRangeProcessing: false);
    }

    [HttpDelete("{attachmentId:guid}")]
    public async Task<IActionResult> Delete(Guid caseId, Guid attachmentId, DeleteAttachmentRequest request)
    {
        var item = await AccessibleCase(caseId); if (item is null) return NotFound();
        var attachment = await db.CaseAttachments.SingleOrDefaultAsync(x => x.Id == attachmentId && x.ServiceRequestId == caseId && !x.IsDeleted); if (attachment is null) return NotFound();
        if (request.Reason?.Trim().Length is not >= 10 or > 500) return BadRequest(new { message = "A deletion reason of 10–500 characters is required." });
        if (!CanDelete(item, attachment)) return NotFound();
        attachment.SoftDelete(User.UserId(), request.Reason, DateTimeOffset.UtcNow);
        db.CaseActivities.Add(new CaseActivity(caseId, User.UserId(), "AttachmentSoftDeleted", $"Attachment soft deleted: {attachment.OriginalFileName}. Reason: {request.Reason.Trim()}", false, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(); return NoContent();
    }

    private async Task<ServiceRequest?> AccessibleCase(Guid id)
    {
        var item = await db.ServiceRequests.SingleOrDefaultAsync(x => x.Id == id); if (item is null) return null;
        if (User.IsInRole(CivicFlowRoles.Resident)) return item.ResidentId == User.UserId() ? item : null;
        if (User.IsInRole(CivicFlowRoles.CaseOfficer)) return item.AssignedOfficerId == User.UserId() ? item : null;
        return User.IsInRole(CivicFlowRoles.TeamManager) || User.IsInRole(CivicFlowRoles.SystemAdministrator) ? item : null;
    }

    private bool CanDelete(ServiceRequest item, CaseAttachment attachment) =>
        ResidentEditableStatuses.Contains(item.Status) &&
        attachment.UploadedByUserId == User.UserId() &&
        (!User.IsInRole(CivicFlowRoles.Resident) || attachment.Visibility == AttachmentVisibility.Public) &&
        (User.IsInRole(CivicFlowRoles.Resident) || User.IsInRole(CivicFlowRoles.CaseOfficer) ||
         User.IsInRole(CivicFlowRoles.TeamManager) || User.IsInRole(CivicFlowRoles.SystemAdministrator));

    private void NotifyUpload(ServiceRequest item, CaseAttachment attachment, CaseActivity activity)
    {
        if (attachment.Visibility == AttachmentVisibility.Internal) return;
        Guid? recipient = User.IsInRole(CivicFlowRoles.Resident) ? item.AssignedOfficerId : item.ResidentId;
        if (recipient.HasValue && recipient != User.UserId())
            db.UserNotifications.Add(new(recipient.Value, item.Id, "Attachment added", $"{item.ReferenceNumber}: A public attachment was added.", DateTimeOffset.UtcNow, activity.Id.ToString()));
    }

    private async Task<bool> CompensateBlobAsync(string storageKey)
    {
        try { await storage.DeleteIfExistsAsync(storageKey, CancellationToken.None); return true; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Attachment blob compensation failed; orphan reconciliation will retry.");
            return false;
        }
    }

    private static AttachmentDto ToDto(CaseAttachment x, bool canDelete) => new(x.Id, x.OriginalFileName, x.ContentType, x.SizeBytes, x.Visibility.ToString(), x.UploadedAtUtc, canDelete);
}

public sealed record AttachmentDto(Guid Id, string OriginalFileName, string ContentType, long SizeBytes, string Visibility, DateTimeOffset UploadedAtUtc, bool CanDelete);
public sealed record DeleteAttachmentRequest(string Reason);
