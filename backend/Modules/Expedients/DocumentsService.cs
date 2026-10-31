using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TecNM.Api.Core.Entities;
using TecNM.Api.Core.Exceptions;
using TecNM.Api.Core.Http;
using TecNM.Api.Core.Security;
using TecNM.Api.Core.Storage;
using TecNM.Api.Data;

namespace TecNM.Api.Modules.Expedients;

public sealed class DocumentsService(
    TecNMDbContext dbContext,
    ICurrentUser currentUser,
    IFileStorage fileStorage)
{
    private const long MaxDocumentSizeBytes = 5 * 1024 * 1024; // 5 MB

    private static readonly HashSet<string> ValidTramiteTypes =
    [
        "social_service",
        "internship",
        "general"
    ];

    private static readonly HashSet<string> ValidStatuses =
    [
        "pending",
        "approved",
        "rejected",
        "superseded"
    ];

    private static readonly Dictionary<string, HashSet<string>> ValidCodesByTramite = new(StringComparer.OrdinalIgnoreCase)
    {
        ["social_service"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SOL", "CP", "CA", "CC", "R1", "R2", "R3", "EV1", "EV2", "EV3", "CT", "COT"
        },
        ["internship"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CP", "CA", "EV", "CT", "CC"
        },
        ["general"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "IMSS", "KARDEX", "CURP", "ACTA", "OTHER"
        }
    };

    public async Task<IReadOnlyList<ExpedientDocumentResponse>> GetStudentDocumentsAsync(
        long studentId,
        string? tramiteType,
        long? tramiteId,
        string? status,
        bool includeSuperseded,
        CancellationToken cancellationToken = default)
    {
        await EnsureCanAccessStudentAsync(studentId, cancellationToken);

        var query = dbContext.ExpedientDocuments
            .Include(d => d.Reviewer)
            .AsNoTracking()
            .Where(d => d.StudentId == studentId);

        if (!includeSuperseded)
        {
            query = query.Where(d => d.Status != "superseded");
        }

        if (!string.IsNullOrWhiteSpace(tramiteType))
        {
            var normalizedTramite = tramiteType.Trim().ToLowerInvariant();
            query = query.Where(d => d.TramiteType == normalizedTramite);
        }

        if (tramiteId.HasValue)
        {
            query = query.Where(d => d.TramiteId == tramiteId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToLowerInvariant();
            query = query.Where(d => d.Status == normalizedStatus);
        }

        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .ThenByDescending(d => d.Id)
            .ToListAsync(cancellationToken);

        return items.Select(ToResponse).ToList();
    }

    public async Task<IReadOnlyList<ExpedientDocumentResponse>> GetMyDocumentsAsync(
        string? tramiteType,
        long? tramiteId,
        string? status,
        bool includeSuperseded,
        CancellationToken cancellationToken = default)
    {
        var studentId = await ResolveCurrentStudentIdAsync(cancellationToken);
        return await GetStudentDocumentsAsync(studentId, tramiteType, tramiteId, status, includeSuperseded, cancellationToken);
    }

    public async Task<ExpedientDocumentResponse> UploadMyDocumentAsync(
        IFormFile? file,
        string tramiteType,
        long? tramiteId,
        string documentCode,
        CancellationToken cancellationToken = default)
    {
        var studentId = await ResolveCurrentStudentIdAsync(cancellationToken);
        return await UploadDocumentAsync(studentId, file, tramiteType, tramiteId, documentCode, cancellationToken);
    }

    public async Task<ExpedientDocumentResponse> UploadDocumentAsync(
        long studentId,
        IFormFile? file,
        string tramiteType,
        long? tramiteId,
        string documentCode,
        CancellationToken cancellationToken = default)
    {
        await EnsureCanAccessStudentAsync(studentId, cancellationToken);

        var normalizedTramite = ValidateUploadParameters(file, tramiteType, tramiteId, documentCode, out var normalizedCode);

        if (normalizedTramite != "general")
        {
            await EnsureTramiteBelongsToStudentAsync(normalizedTramite, tramiteId!.Value, studentId, cancellationToken);
        }

        var existingDocs = await dbContext.ExpedientDocuments
            .Where(d => d.StudentId == studentId
                        && d.TramiteType == normalizedTramite
                        && d.TramiteId == tramiteId
                        && d.DocumentCode == normalizedCode
                        && d.Status != "superseded")
            .ToListAsync(cancellationToken);

        if (existingDocs.Any(d => d.Status == "approved"))
        {
            throw new ConflictException(
                "documents.already_approved",
                $"El documento '{normalizedCode}' ya fue aprobado previamente y no puede ser reemplazado.");
        }

        var relativeDir = $"{studentId}/{normalizedTramite}";
        var fileGuid = Guid.NewGuid().ToString("N");
        var storagePath = await fileStorage.SavePdfAsync(
            file!,
            relativeDir,
            fileGuid,
            MaxDocumentSizeBytes,
            cancellationToken);

        foreach (var prev in existingDocs)
        {
            prev.Status = "superseded";
        }

        var safeOriginalName = Path.GetFileName(file!.FileName);
        if (string.IsNullOrWhiteSpace(safeOriginalName))
        {
            safeOriginalName = $"{normalizedCode}.pdf";
        }
        else if (safeOriginalName.Length > 255)
        {
            safeOriginalName = safeOriginalName[..255];
        }

        var entity = new ExpedientDocument
        {
            StudentId = studentId,
            TramiteType = normalizedTramite,
            TramiteId = tramiteId,
            DocumentCode = normalizedCode,
            OriginalFileName = safeOriginalName,
            StoragePath = storagePath,
            FileMimeType = "application/pdf",
            FileSizeBytes = file.Length,
            Status = "pending"
        };

        dbContext.ExpedientDocuments.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(entity);
    }

    public async Task<(Stream Stream, string FileName)> GetDocumentStreamAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var doc = await dbContext.ExpedientDocuments
            .Include(d => d.Student)
            .AsNoTracking()
            .SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("documents.not_found", $"Documento con id {id} no encontrado.");

        await EnsureCanAccessStudentAsync(doc.StudentId, cancellationToken);

        var stream = await fileStorage.OpenReadAsync(doc.StoragePath, cancellationToken);
        var controlNumber = doc.Student?.ControlNumber ?? doc.StudentId.ToString();
        var downloadName = $"{controlNumber}_{doc.DocumentCode}_{doc.Id}.pdf";

        return (stream, downloadName);
    }

    public async Task<ExpedientDocumentResponse> ReviewDocumentAsync(
        long id,
        ReviewDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var doc = await dbContext.ExpedientDocuments
            .Include(d => d.Student)
            .SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("documents.not_found", $"Documento con id {id} no encontrado.");

        EnsureCanReviewTramiteType(doc.TramiteType);

        if (doc.Status != "pending")
        {
            throw new ConflictException(
                "documents.invalid_transition",
                $"Solo se pueden revisar documentos en estado 'pending'. Estado actual: '{doc.Status}'.");
        }

        var normalizedStatus = request.Status?.Trim().ToLowerInvariant() ?? string.Empty;
        var errors = new Dictionary<string, string[]>();

        if (normalizedStatus is not ("approved" or "rejected"))
        {
            errors["status"] = ["El estado de revisión debe ser 'approved' o 'rejected'."];
        }

        var trimmedReason = string.IsNullOrWhiteSpace(request.RejectionReason)
            ? null
            : request.RejectionReason.Trim();

        if (normalizedStatus == "rejected")
        {
            if (string.IsNullOrWhiteSpace(trimmedReason))
            {
                errors["rejectionReason"] = ["El motivo de rechazo es obligatorio cuando se rechaza un documento."];
            }
            else if (trimmedReason.Length > 1000)
            {
                errors["rejectionReason"] = ["El motivo de rechazo no puede exceder 1000 caracteres."];
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        doc.Status = normalizedStatus;
        doc.RejectionReason = normalizedStatus == "rejected" ? trimmedReason : null;
        doc.ReviewedBy = currentUser.Id;
        doc.ReviewedAt = DateTimeOffset.UtcNow;

        await SyncMilestoneOnReviewAsync(doc, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var updated = await dbContext.ExpedientDocuments
            .Include(d => d.Reviewer)
            .AsNoTracking()
            .SingleAsync(d => d.Id == id, cancellationToken);

        return ToResponse(updated);
    }

    public async Task DeleteDocumentAsync(long id, CancellationToken cancellationToken = default)
    {
        var doc = await dbContext.ExpedientDocuments
            .SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("documents.not_found", $"Documento con id {id} no encontrado.");

        await EnsureCanAccessStudentAsync(doc.StudentId, cancellationToken);

        if (doc.Status != "pending")
        {
            throw new ConflictException(
                "documents.not_pending",
                "Únicamente se pueden eliminar documentos que aún se encuentran en estado pendiente de revisión ('pending').");
        }

        dbContext.ExpedientDocuments.Remove(doc);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResponse<ReviewQueueDocumentResponse>> GetReviewQueueAsync(
        int page,
        int pageSize,
        string? tramiteType,
        string? documentCode,
        long? periodId,
        string? status,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.ExpedientDocuments
            .Include(d => d.Student)
                .ThenInclude(s => s!.Career)
            .Include(d => d.Reviewer)
            .AsNoTracking()
            .Where(d => d.Status != "superseded");

        var targetStatus = string.IsNullOrWhiteSpace(status) ? "pending" : status.Trim().ToLowerInvariant();
        if (ValidStatuses.Contains(targetStatus))
        {
            query = query.Where(d => d.Status == targetStatus);
        }

        // Restricción por rol si no es admin
        var isAdmin = currentUser.IsInRole("admin");
        var isSsOfficer = currentUser.IsInRole("social_service_officer");
        var isIntCoord = currentUser.IsInRole("internship_coordinator");

        if (!string.IsNullOrWhiteSpace(tramiteType))
        {
            var normalizedTramite = tramiteType.Trim().ToLowerInvariant();
            EnsureCanReviewTramiteType(normalizedTramite);
            query = query.Where(d => d.TramiteType == normalizedTramite);
        }
        else if (!isAdmin)
        {
            if (isSsOfficer && !isIntCoord)
            {
                query = query.Where(d => d.TramiteType == "social_service" || d.TramiteType == "general");
            }
            else if (isIntCoord && !isSsOfficer)
            {
                query = query.Where(d => d.TramiteType == "internship" || d.TramiteType == "general");
            }
        }

        if (!string.IsNullOrWhiteSpace(documentCode))
        {
            var normalizedCode = documentCode.Trim().ToUpperInvariant();
            query = query.Where(d => d.DocumentCode == normalizedCode);
        }

        if (periodId.HasValue)
        {
            var ssIds = await dbContext.Database
                .SqlQueryRaw<long>(
                    "SELECT id AS \"Value\" FROM social_services WHERE period_id = {0} AND deleted_at IS NULL",
                    periodId.Value)
                .ToListAsync(cancellationToken);

            var intIds = await dbContext.Database
                .SqlQueryRaw<long>(
                    "SELECT id AS \"Value\" FROM internships WHERE period_id = {0} AND deleted_at IS NULL",
                    periodId.Value)
                .ToListAsync(cancellationToken);

            query = query.Where(d =>
                (d.TramiteType == "social_service" && d.TramiteId.HasValue && ssIds.Contains(d.TramiteId.Value)) ||
                (d.TramiteType == "internship" && d.TramiteId.HasValue && intIds.Contains(d.TramiteId.Value)));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            if (term.Length > 100)
            {
                term = term[..100];
            }

            var pattern = $"%{term}%";
            query = query.Where(d =>
                d.Student != null && (
                    EF.Functions.ILike(d.Student.ControlNumber, pattern) ||
                    EF.Functions.ILike(d.Student.FirstName, pattern) ||
                    EF.Functions.ILike(d.Student.FirstSurname, pattern) ||
                    (d.Student.SecondSurname != null && EF.Functions.ILike(d.Student.SecondSurname, pattern))));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalItems / (double)normalizedSize);

        var items = await query
            .OrderBy(d => d.CreatedAt)
            .ThenBy(d => d.Id)
            .Skip((normalizedPage - 1) * normalizedSize)
            .Take(normalizedSize)
            .ToListAsync(cancellationToken);

        var responses = items.Select(ToReviewQueueResponse).ToList();
        return new PagedResponse<ReviewQueueDocumentResponse>(
            responses,
            normalizedPage,
            normalizedSize,
            totalItems,
            totalPages);
    }

    private async Task<long> ResolveCurrentStudentIdAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.Id.HasValue)
        {
            throw new NotFoundException("students.not_found", "No se encontró un perfil de estudiante para la sesión actual.");
        }

        var studentId = await dbContext.Students
            .AsNoTracking()
            .Where(s => s.UserId == currentUser.Id.Value)
            .Select(s => (long?)s.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (!studentId.HasValue)
        {
            throw new NotFoundException("students.not_found", "El usuario actual no tiene un expediente de estudiante vinculado.");
        }

        return studentId.Value;
    }

    private async Task EnsureCanAccessStudentAsync(long studentId, CancellationToken cancellationToken)
    {
        var student = await dbContext.Students
            .AsNoTracking()
            .Where(s => s.Id == studentId)
            .Select(s => new { s.Id, s.UserId })
            .SingleOrDefaultAsync(cancellationToken);

        if (student is null)
        {
            throw new NotFoundException("students.not_found", $"Estudiante con id {studentId} no encontrado.");
        }

        var isStaffOrViewer = currentUser.IsInRole("admin")
                              || currentUser.IsInRole("social_service_officer")
                              || currentUser.IsInRole("internship_coordinator")
                              || currentUser.IsInRole("department_head");

        if (!isStaffOrViewer)
        {
            if (!currentUser.Id.HasValue || student.UserId != currentUser.Id.Value)
            {
                // Regla de api-conventions: Recurso ajeno responde 404 para evitar enumeración
                throw new NotFoundException("documents.not_found", "El expediente solicitado no existe.");
            }
        }
    }

    private void EnsureCanReviewTramiteType(string tramiteType)
    {
        if (currentUser.IsInRole("admin"))
        {
            return;
        }

        var normalized = tramiteType.Trim().ToLowerInvariant();
        if (normalized == "social_service" && currentUser.IsInRole("social_service_officer"))
        {
            return;
        }

        if (normalized == "internship" && currentUser.IsInRole("internship_coordinator"))
        {
            return;
        }

        if (normalized == "general" &&
            (currentUser.IsInRole("social_service_officer") || currentUser.IsInRole("internship_coordinator")))
        {
            return;
        }

        throw new AppException(
            StatusCodes.Status403Forbidden,
            "auth.forbidden",
            $"No cuenta con permisos para revisar documentos del trámite '{normalized}'.",
            "Acceso denegado");
    }

    private static string ValidateUploadParameters(
        IFormFile? file,
        string? tramiteType,
        long? tramiteId,
        string? documentCode,
        out string normalizedCode)
    {
        var errors = new Dictionary<string, string[]>();

        if (file is null || file.Length == 0)
        {
            errors["file"] = ["El archivo PDF es obligatorio."];
        }

        var normalizedTramite = tramiteType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!ValidTramiteTypes.Contains(normalizedTramite))
        {
            errors["tramiteType"] = ["El tipo de trámite debe ser 'social_service', 'internship' o 'general'."];
        }

        normalizedCode = documentCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedCode) || normalizedCode.Length > 64)
        {
            errors["documentCode"] = ["El código de documento es obligatorio (máx. 64 caracteres)."];
        }
        else if (ValidCodesByTramite.TryGetValue(normalizedTramite, out var allowedCodes) && !allowedCodes.Contains(normalizedCode))
        {
            errors["documentCode"] = [$"El código '{normalizedCode}' no es válido para el trámite '{normalizedTramite}'. Permitidos: {string.Join(", ", allowedCodes)}."];
        }

        if (normalizedTramite == "general" && tramiteId.HasValue)
        {
            errors["tramiteId"] = ["Los documentos de tipo 'general' no deben incluir tramiteId."];
        }
        else if (normalizedTramite is "social_service" or "internship" && (!tramiteId.HasValue || tramiteId.Value <= 0))
        {
            errors["tramiteId"] = [$"El tramiteId es obligatorio cuando tramiteType es '{normalizedTramite}'."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        return normalizedTramite;
    }

    private async Task EnsureTramiteBelongsToStudentAsync(
        string tramiteType,
        long tramiteId,
        long studentId,
        CancellationToken cancellationToken)
    {
        bool exists;
        if (tramiteType == "social_service")
        {
            exists = await dbContext.Database
                .SqlQueryRaw<int>(
                    "SELECT 1 AS \"Value\" FROM social_services WHERE id = {0} AND student_id = {1} AND deleted_at IS NULL",
                    tramiteId,
                    studentId)
                .AnyAsync(cancellationToken);
        }
        else
        {
            exists = await dbContext.Database
                .SqlQueryRaw<int>(
                    "SELECT 1 AS \"Value\" FROM internships WHERE id = {0} AND student_id = {1} AND deleted_at IS NULL",
                    tramiteId,
                    studentId)
                .AnyAsync(cancellationToken);
        }

        if (!exists)
        {
            throw new BusinessRuleException(
                "documents.tramite_mismatch",
                $"El trámite indicado ({tramiteType} #{tramiteId}) no existe o no pertenece al estudiante #{studentId}.");
        }
    }

    private async Task SyncMilestoneOnReviewAsync(ExpedientDocument doc, CancellationToken cancellationToken)
    {
        if (doc.TramiteType == "general" && doc.DocumentCode == "IMSS" && doc.Student is not null)
        {
            if (doc.Status == "approved")
            {
                doc.Student.HasValidImss = true;
                doc.Student.ImssValidatedAt = DateTime.UtcNow;
            }
            else if (doc.Status == "rejected")
            {
                doc.Student.HasValidImss = false;
                doc.Student.ImssValidatedAt = null;
            }

            return;
        }

        if (doc.Status != "approved" || !doc.TramiteId.HasValue)
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (doc.TramiteType == "social_service")
        {
            if (doc.DocumentCode == "CA")
            {
                await dbContext.Database.ExecuteSqlRawAsync(
                    "UPDATE social_services SET acceptance_letter_date = COALESCE(acceptance_letter_date, {0}), updated_at = CURRENT_TIMESTAMP WHERE id = {1} AND deleted_at IS NULL",
                    [today, doc.TramiteId.Value],
                    cancellationToken);
            }
            else if (doc.DocumentCode == "CT")
            {
                await dbContext.Database.ExecuteSqlRawAsync(
                    "UPDATE social_services SET termination_letter_date = COALESCE(termination_letter_date, {0}), updated_at = CURRENT_TIMESTAMP WHERE id = {1} AND deleted_at IS NULL",
                    [today, doc.TramiteId.Value],
                    cancellationToken);
            }
        }
        else if (doc.TramiteType == "internship")
        {
            if (doc.DocumentCode == "CA")
            {
                await dbContext.Database.ExecuteSqlRawAsync(
                    "UPDATE internships SET acceptance_letter_date = COALESCE(acceptance_letter_date, {0}), updated_at = CURRENT_TIMESTAMP WHERE id = {1} AND deleted_at IS NULL",
                    [today, doc.TramiteId.Value],
                    cancellationToken);
            }
            else if (doc.DocumentCode == "CT")
            {
                await dbContext.Database.ExecuteSqlRawAsync(
                    "UPDATE internships SET termination_letter_date = COALESCE(termination_letter_date, {0}), updated_at = CURRENT_TIMESTAMP WHERE id = {1} AND deleted_at IS NULL",
                    [today, doc.TramiteId.Value],
                    cancellationToken);
            }
        }
    }

    private static ExpedientDocumentResponse ToResponse(ExpedientDocument d)
    {
        string? reviewerName = null;
        if (d.Reviewer is not null)
        {
            reviewerName = string.Join(
                " ",
                new[] { d.Reviewer.FirstName, d.Reviewer.FirstSurname, d.Reviewer.SecondSurname }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        return new ExpedientDocumentResponse(
            d.Id,
            d.StudentId,
            d.TramiteType,
            d.TramiteId,
            d.DocumentCode,
            d.OriginalFileName,
            d.FileMimeType,
            d.FileSizeBytes,
            d.Status,
            d.RejectionReason,
            reviewerName,
            d.ReviewedAt,
            d.CreatedAt);
    }

    private static ReviewQueueDocumentResponse ToReviewQueueResponse(ExpedientDocument d)
    {
        var baseResp = ToResponse(d);
        var student = d.Student;
        var fullName = student is null
            ? "Sin nombre"
            : string.Join(" ", new[] { student.FirstName, student.FirstSurname, student.SecondSurname }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

        return new ReviewQueueDocumentResponse(
            baseResp.Id,
            baseResp.StudentId,
            student?.ControlNumber ?? string.Empty,
            fullName,
            student?.Career?.Code ?? string.Empty,
            student?.Career?.Name ?? string.Empty,
            baseResp.TramiteType,
            baseResp.TramiteId,
            baseResp.DocumentCode,
            baseResp.OriginalFileName,
            baseResp.FileMimeType,
            baseResp.FileSizeBytes,
            baseResp.Status,
            baseResp.RejectionReason,
            baseResp.ReviewedByName,
            baseResp.ReviewedAt,
            baseResp.CreatedAt);
    }
}
