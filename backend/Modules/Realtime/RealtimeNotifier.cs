using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TecNM.Api.Data;

namespace TecNM.Api.Modules.Realtime;

public interface IRealtimeNotifier
{
    Task NotifyDocumentUploadedAsync(
        long studentId,
        string tramiteType,
        long? tramiteId,
        long documentId,
        string documentCode,
        long? actorUserId,
        CancellationToken cancellationToken = default);

    Task NotifyDocumentReviewedAsync(
        long studentId,
        string tramiteType,
        long? tramiteId,
        long documentId,
        string documentCode,
        string status,
        string? rejectionReason,
        long? actorUserId,
        CancellationToken cancellationToken = default);

    Task NotifyDocumentDeletedAsync(
        long studentId,
        string tramiteType,
        long? tramiteId,
        long documentId,
        string documentCode,
        long? actorUserId,
        CancellationToken cancellationToken = default);

    Task NotifyTramiteUpdatedAsync(
        long studentId,
        string tramiteType,
        long tramiteId,
        string status,
        string staffTitle,
        string staffSummary,
        string? studentTitle,
        string? studentMessage,
        string tone,
        long? actorUserId,
        CancellationToken cancellationToken = default);

    Task NotifyNonConformanceUpdatedAsync(
        long studentId,
        string tramiteType,
        long tramiteId,
        string incidentCode,
        string status,
        long? actorUserId,
        CancellationToken cancellationToken = default);
}

public sealed class NullRealtimeNotifier : IRealtimeNotifier
{
    public static readonly NullRealtimeNotifier Instance = new();

    public Task NotifyDocumentUploadedAsync(long studentId, string tramiteType, long? tramiteId, long documentId, string documentCode, long? actorUserId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task NotifyDocumentReviewedAsync(long studentId, string tramiteType, long? tramiteId, long documentId, string documentCode, string status, string? rejectionReason, long? actorUserId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task NotifyDocumentDeletedAsync(long studentId, string tramiteType, long? tramiteId, long documentId, string documentCode, long? actorUserId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task NotifyTramiteUpdatedAsync(long studentId, string tramiteType, long tramiteId, string status, string staffTitle, string staffSummary, string? studentTitle, string? studentMessage, string tone, long? actorUserId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task NotifyNonConformanceUpdatedAsync(long studentId, string tramiteType, long tramiteId, string incidentCode, string status, long? actorUserId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class SignalRRealtimeNotifier(
    TecNMDbContext dbContext,
    IHubContext<SsppHub> hubContext,
    DocumentReviewPresenceRegistry presenceRegistry,
    TimeProvider clock,
    ILogger<SignalRRealtimeNotifier> logger) : IRealtimeNotifier
{
    private sealed record StudentBrief(long Id, long? UserId, string ControlNumber, string FullName);

    public async Task NotifyDocumentUploadedAsync(
        long studentId,
        string tramiteType,
        long? tramiteId,
        long documentId,
        string documentCode,
        long? actorUserId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var student = await LoadStudentBriefAsync(studentId, cancellationToken);
            var label = student is null ? $"Alumno #{studentId}" : $"{student.ControlNumber} · {student.FullName}";
            var processName = FormatProcessName(tramiteType);
            var payload = new RealtimeEventPayload(
                EventType: "document.uploaded",
                TramiteType: tramiteType,
                TramiteId: tramiteId,
                StudentId: studentId,
                DocumentId: documentId,
                DocumentCode: documentCode,
                Status: "pending",
                Title: $"Nuevo PDF en cola ({processName})",
                Message: $"{label} cargó el documento {documentCode} para validación.",
                Tone: "info",
                Silent: false,
                ActorUserId: actorUserId,
                OccurredAt: clock.GetUtcNow());

            await hubContext.Clients
                .Group(RealtimeGroups.Staff(tramiteType))
                .SendAsync("SsppEvent", payload, cancellationToken);

            if (tramiteType == "internship" && documentCode == "CA" && student?.UserId is long studentUserId)
            {
                var studentPayload = payload with
                {
                    Title = "Oficio de Aceptación registrado",
                    Message = "Vinculación incorporó el PDF de tu Oficio de Aceptación (CA) al expediente."
                };
                await hubContext.Clients
                    .Group(RealtimeGroups.User(studentUserId))
                    .SendAsync("SsppEvent", studentPayload, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SignalR notification failed for document.uploaded #{DocumentId}.", documentId);
        }
    }

    public async Task NotifyDocumentReviewedAsync(
        long studentId,
        string tramiteType,
        long? tramiteId,
        long documentId,
        string documentCode,
        string status,
        string? rejectionReason,
        long? actorUserId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var releasedType = presenceRegistry.ReleaseDocument(documentId);
            if (releasedType is not null)
            {
                var locks = presenceRegistry.GetLocksForTramiteType(releasedType);
                await hubContext.Clients
                    .Group(RealtimeGroups.Staff(releasedType))
                    .SendAsync("ReviewPresenceUpdated", releasedType, locks, cancellationToken);
            }

            var student = await LoadStudentBriefAsync(studentId, cancellationToken);
            var label = student is null ? $"Alumno #{studentId}" : $"{student.ControlNumber} · {student.FullName}";
            var isApproved = status == "approved";
            var now = clock.GetUtcNow();

            var staffPayload = new RealtimeEventPayload(
                EventType: "document.reviewed",
                TramiteType: tramiteType,
                TramiteId: tramiteId,
                StudentId: studentId,
                DocumentId: documentId,
                DocumentCode: documentCode,
                Status: status,
                Title: isApproved ? $"Documento {documentCode} aprobado" : $"Documento {documentCode} observado",
                Message: $"{label} — dictamen registrado ({(isApproved ? "Aprobado" : "Observado")}).",
                Tone: isApproved ? "success" : "warning",
                Silent: false,
                ActorUserId: actorUserId,
                OccurredAt: now);

            await hubContext.Clients
                .Group(RealtimeGroups.Staff(tramiteType))
                .SendAsync("SsppEvent", staffPayload, cancellationToken);

            if (student?.UserId is long studentUserId)
            {
                var studentMsg = isApproved
                    ? $"Tu documento {documentCode} fue aprobado por Vinculación."
                    : string.IsNullOrWhiteSpace(rejectionReason)
                        ? $"Tu documento {documentCode} fue observado. Revisa tu expediente."
                        : $"Tu documento {documentCode} requiere corrección: {Truncate(rejectionReason, 120)}";

                var studentPayload = staffPayload with
                {
                    Title = isApproved ? "Documento aprobado" : "Documento con observaciones",
                    Message = studentMsg
                };

                await hubContext.Clients
                    .Group(RealtimeGroups.User(studentUserId))
                    .SendAsync("SsppEvent", studentPayload, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SignalR notification failed for document.reviewed #{DocumentId}.", documentId);
        }
    }

    public async Task NotifyDocumentDeletedAsync(
        long studentId,
        string tramiteType,
        long? tramiteId,
        long documentId,
        string documentCode,
        long? actorUserId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var releasedType = presenceRegistry.ReleaseDocument(documentId);
            if (releasedType is not null)
            {
                var locks = presenceRegistry.GetLocksForTramiteType(releasedType);
                await hubContext.Clients
                    .Group(RealtimeGroups.Staff(releasedType))
                    .SendAsync("ReviewPresenceUpdated", releasedType, locks, cancellationToken);
            }

            var payload = new RealtimeEventPayload(
                EventType: "document.deleted",
                TramiteType: tramiteType,
                TramiteId: tramiteId,
                StudentId: studentId,
                DocumentId: documentId,
                DocumentCode: documentCode,
                Status: "deleted",
                Title: "Documento retirado",
                Message: $"Se retiró el documento pendiente {documentCode}.",
                Tone: "info",
                Silent: true,
                ActorUserId: actorUserId,
                OccurredAt: clock.GetUtcNow());

            await hubContext.Clients
                .Group(RealtimeGroups.Staff(tramiteType))
                .SendAsync("SsppEvent", payload, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SignalR notification failed for document.deleted #{DocumentId}.", documentId);
        }
    }

    public async Task NotifyTramiteUpdatedAsync(
        long studentId,
        string tramiteType,
        long tramiteId,
        string status,
        string staffTitle,
        string staffSummary,
        string? studentTitle,
        string? studentMessage,
        string tone,
        long? actorUserId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var student = await LoadStudentBriefAsync(studentId, cancellationToken);
            var label = student is null ? $"Alumno #{studentId}" : $"{student.ControlNumber} · {student.FullName}";
            var now = clock.GetUtcNow();

            var staffPayload = new RealtimeEventPayload(
                EventType: "tramite.updated",
                TramiteType: tramiteType,
                TramiteId: tramiteId,
                StudentId: studentId,
                DocumentId: null,
                DocumentCode: null,
                Status: status,
                Title: staffTitle,
                Message: $"{label} — {staffSummary}",
                Tone: tone,
                Silent: false,
                ActorUserId: actorUserId,
                OccurredAt: now);

            await hubContext.Clients
                .Group(RealtimeGroups.Staff(tramiteType))
                .SendAsync("SsppEvent", staffPayload, cancellationToken);

            if (student?.UserId is long studentUserId && !string.IsNullOrWhiteSpace(studentMessage))
            {
                var studentPayload = staffPayload with
                {
                    Title = string.IsNullOrWhiteSpace(studentTitle) ? staffTitle : studentTitle,
                    Message = studentMessage
                };

                await hubContext.Clients
                    .Group(RealtimeGroups.User(studentUserId))
                    .SendAsync("SsppEvent", studentPayload, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SignalR notification failed for tramite.updated {TramiteType} #{TramiteId}.", tramiteType, tramiteId);
        }
    }

    public async Task NotifyNonConformanceUpdatedAsync(
        long studentId,
        string tramiteType,
        long tramiteId,
        string incidentCode,
        string status,
        long? actorUserId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var student = await LoadStudentBriefAsync(studentId, cancellationToken);
            var label = student is null ? $"Alumno #{studentId}" : $"{student.ControlNumber} · {student.FullName}";
            var statusLabel = status switch
            {
                "open" => "Abierto",
                "under_review" => "En revisión",
                "resolved" => "Resuelto",
                "cancelled" => "Cancelado",
                _ => status
            };
            var tone = status == "resolved" ? "success" : status == "open" ? "danger" : "warning";

            var payload = new RealtimeEventPayload(
                EventType: "quality.pnc_updated",
                TramiteType: tramiteType,
                TramiteId: tramiteId,
                StudentId: studentId,
                DocumentId: null,
                DocumentCode: incidentCode,
                Status: status,
                Title: $"PNC {incidentCode} · {statusLabel}",
                Message: $"{label} — actualización en bitácora ISO 9001 ({statusLabel}).",
                Tone: tone,
                Silent: false,
                ActorUserId: actorUserId,
                OccurredAt: clock.GetUtcNow());

            await hubContext.Clients
                .Group(RealtimeGroups.Staff(tramiteType))
                .SendAsync("SsppEvent", payload, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SignalR notification failed for quality.pnc_updated {IncidentCode}.", incidentCode);
        }
    }

    private async Task<StudentBrief?> LoadStudentBriefAsync(long studentId, CancellationToken ct)
    {
        var row = await dbContext.Students
            .AsNoTracking()
            .Where(s => s.Id == studentId)
            .Select(s => new { s.Id, s.UserId, s.ControlNumber, s.FirstName, s.FirstSurname, s.SecondSurname })
            .SingleOrDefaultAsync(ct);

        if (row is null) return null;
        var fullName = string.Join(" ", new[] { row.FirstName, row.FirstSurname, row.SecondSurname }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        return new StudentBrief(row.Id, row.UserId, row.ControlNumber, fullName);
    }

    private static string FormatProcessName(string tramiteType) =>
        tramiteType switch
        {
            "social_service" => "Servicio Social",
            "internship" => "Prácticas",
            _ => "Expediente General"
        };

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : $"{value[..maxLength]}…";
}
