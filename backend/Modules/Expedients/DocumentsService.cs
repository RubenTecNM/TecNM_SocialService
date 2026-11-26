using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TecNM.Api.Core.Entities;
using TecNM.Api.Core.Exceptions;
using TecNM.Api.Core.Http;
using TecNM.Api.Core.Security;
using TecNM.Api.Core.Storage;
using TecNM.Api.Data;
using TecNM.Api.Modules.Realtime;

namespace TecNM.Api.Modules.Expedients;

public sealed class DocumentsService(
    TecNMDbContext dbContext,
    ICurrentUser currentUser,
    IFileStorage fileStorage,
    TimeProvider clock,
    IRealtimeNotifier? realtime = null)
{
    private readonly IRealtimeNotifier _realtime = realtime ?? NullRealtimeNotifier.Instance;
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
            "SOL", "CP", "CA", "CC", "R1", "R2", "R3", "EV1", "EV2", "EV3", "CT", "COT", "EV_SUP1", "EV_SUP2", "EV_SUP3", "EV_SELF1", "EV_SELF2", "EV_SELF3", "EV_ACT"
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
        await currentUser.RequireStudentAsync(dbContext, studentId, cancellationToken, true);

        var query = currentUser.VisibleDocuments(dbContext, dbContext.ExpedientDocuments)
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
            .OrderBy(SocialServiceDocumentCatalog.SortExpression())
            .ThenByDescending(d => d.CreatedAt)
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
        CancellationToken cancellationToken = default,
        DateOnly? documentDate = null)
    {
        var studentId = await ResolveCurrentStudentIdAsync(cancellationToken);
        return await UploadDocumentAsync(studentId, file, tramiteType, tramiteId, documentCode, cancellationToken, documentDate);
    }

    public async Task<ExpedientDocumentResponse> UploadDocumentAsync(
        long studentId,
        IFormFile? file,
        string tramiteType,
        long? tramiteId,
        string documentCode,
        CancellationToken cancellationToken = default,
        DateOnly? documentDate = null)
    {
        await currentUser.RequireStudentAsync(dbContext, studentId, cancellationToken, true);

        var normalizedTramite = ValidateUploadParameters(file, tramiteType, tramiteId, documentCode, out var normalizedCode);
        await EnsureEvidenceOwnerAsync(studentId, normalizedTramite, normalizedCode, cancellationToken);
        if (normalizedTramite == "general" && normalizedCode == "IMSS" && documentDate > ImssEligibilityService.Today(clock))
            throw new ValidationException(new Dictionary<string, string[]> { ["documentDate"] = ["La fecha de expedición no puede ser futura."] });

        if (normalizedTramite != "general")
        {
            await EnsureTramiteBelongsToStudentAsync(normalizedTramite, tramiteId!.Value, studentId, cancellationToken);
        }

        await EnsureCanChangeEvidenceAsync(normalizedTramite, tramiteId, normalizedCode, cancellationToken);
        await using var hashStream = file!.OpenReadStream();
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(hashStream, cancellationToken));
        await using var transaction = dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken) : null;
        var existingDocs = await dbContext.ExpedientDocuments
            .Where(d => d.StudentId == studentId
                        && d.TramiteType == normalizedTramite
                        && d.TramiteId == tramiteId
                        && d.DocumentCode == normalizedCode
                        && d.Status != "superseded")
            .ToListAsync(cancellationToken);

        var identical = existingDocs.FirstOrDefault(d => d.ContentSha256 == hash);
        if (identical is not null) return ToResponse(identical);
        if (existingDocs.Any(d => d.Status == "approved") && !(normalizedTramite == "general" && normalizedCode == "IMSS"))
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

        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch { fileStorage.TryDelete(storagePath); throw; }

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
            DocumentDate = documentDate,
            ContentSha256 = hash,
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
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            if (normalizedTramite == "general" && normalizedCode == "IMSS")
            {
                var student = await dbContext.Students.SingleAsync(t => t.Id == studentId, cancellationToken);
                student.ImssValidityFileUrl = storagePath;
                student.HasValidImss = false;
                student.ImssValidatedAt = null;
                student.ImssValidatedBy = null;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
        catch { fileStorage.TryDelete(storagePath); throw; }

        if (transaction is not null)
        {
            await _realtime.NotifyDocumentUploadedAsync(
                studentId,
                normalizedTramite,
                tramiteId,
                entity.Id,
                normalizedCode,
                currentUser.Id,
                cancellationToken);
        }

        return ToResponse(entity);
    }

    public async Task<(Stream Stream, string FileName)> GetDocumentStreamAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var doc = await currentUser.VisibleDocuments(dbContext, dbContext.ExpedientDocuments)
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

    public async Task<ImssDateSuggestion> GetImssDateSuggestionAsync(long id, CancellationToken ct)
    {
        var doc = await currentUser.VisibleDocuments(dbContext, dbContext.ExpedientDocuments).AsNoTracking()
            .SingleOrDefaultAsync(d => d.Id == id && d.TramiteType == "general" && d.DocumentCode == "IMSS" && d.TramiteId == null, ct)
            ?? throw new NotFoundException("documents.not_found", "No existe una constancia IMSS accesible.");
        EnsureCanReviewTramiteType(doc.TramiteType);
        await using var stream = await fileStorage.OpenReadAsync(doc.StoragePath, ct);
        return ImssDateExtractionService.Extract(stream, ct);
    }

    public async Task<ExpedientDocumentResponse> ReviewDocumentAsync(
        long id,
        ReviewDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var doc = await currentUser.VisibleDocuments(dbContext, dbContext.ExpedientDocuments)
            .Include(d => d.Student)
            .SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("documents.not_found", $"Documento con id {id} no encontrado.");

        EnsureCanReviewTramiteType(doc.TramiteType);

        if (doc.Status == request.Status && doc.RejectionReason == (request.Status == "rejected" ? request.RejectionReason?.Trim() : null)) return ToResponse(doc);
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
        if (doc.TramiteType == "social_service" && doc.DocumentCode == "CC")
        {
            var commitment = await dbContext.SocialServiceCommitments.SingleOrDefaultAsync(c => c.SocialServiceId == doc.TramiteId, cancellationToken);
            if (commitment is not null)
            {
                commitment.IsSignedByStudent = doc.Status == "approved";
                commitment.SignedAt = doc.Status == "approved" ? doc.ReviewedAt : null;
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await _realtime.NotifyDocumentReviewedAsync(
            doc.StudentId,
            doc.TramiteType,
            doc.TramiteId,
            doc.Id,
            doc.DocumentCode,
            doc.Status,
            doc.RejectionReason,
            currentUser.Id,
            cancellationToken);

        var updated = await dbContext.ExpedientDocuments
            .Include(d => d.Reviewer)
            .AsNoTracking()
            .SingleAsync(d => d.Id == id, cancellationToken);

        return ToResponse(updated);
    }

    public async Task DeleteDocumentAsync(long id, CancellationToken cancellationToken = default)
    {
        var doc = await currentUser.VisibleDocuments(dbContext, dbContext.ExpedientDocuments)
            .SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("documents.not_found", $"Documento con id {id} no encontrado.");

        await EnsureCanAccessStudentAsync(doc.StudentId, cancellationToken);
        await EnsureEvidenceOwnerAsync(doc.StudentId, doc.TramiteType, doc.DocumentCode, cancellationToken);
        await EnsureCanChangeEvidenceAsync(doc.TramiteType, doc.TramiteId, doc.DocumentCode, cancellationToken);

        if (doc.Status != "pending")
        {
            throw new ConflictException(
                "documents.not_pending",
                "Únicamente se pueden eliminar documentos que aún se encuentran en estado pendiente de revisión ('pending').");
        }

        dbContext.ExpedientDocuments.Remove(doc);
        await dbContext.SaveChangesAsync(cancellationToken);

        await _realtime.NotifyDocumentDeletedAsync(
            doc.StudentId,
            doc.TramiteType,
            doc.TramiteId,
            doc.Id,
            doc.DocumentCode,
            currentUser.Id,
            cancellationToken);
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

        var query = currentUser.VisibleDocuments(dbContext, dbContext.ExpedientDocuments)
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
            var ssIds = dbContext.SocialServices.Where(t => t.PeriodId == periodId.Value).Select(t => t.Id);
            var intIds = dbContext.Internships.Where(t => t.PeriodId == periodId.Value).Select(t => t.Id);
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
                EF.Functions.ILike(d.DocumentCode, pattern) ||
                (d.Student != null && (
                    EF.Functions.ILike(d.Student.ControlNumber, pattern) ||
                    EF.Functions.ILike(d.Student.FirstName, pattern) ||
                    EF.Functions.ILike(d.Student.FirstSurname, pattern) ||
                    (d.Student.SecondSurname != null && EF.Functions.ILike(d.Student.SecondSurname, pattern)) ||
                    (d.Student.Career != null && EF.Functions.ILike(d.Student.Career.Code, pattern)))));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalItems / (double)normalizedSize);

        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .ThenByDescending(d => d.Id)
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

    private async Task EnsureEvidenceOwnerAsync(long studentId, string type, string code, CancellationToken ct)
    {
        if (type == "internship" && code == "CA" && (currentUser.IsInRole("admin") || currentUser.IsInRole("internship_coordinator"))) return;
        if (!currentUser.IsInRole("student") || !await dbContext.Students.AnyAsync(s => s.Id == studentId && s.UserId == currentUser.Id, ct))
            throw new AppException(403, "documents.upload_forbidden", "Solo el alumno propietario puede modificar esta evidencia.");
    }

    private async Task EnsureCanChangeEvidenceAsync(string type, long? tramiteId, string code, CancellationToken ct)
    {
        if (type == "general")
        {
            if (!currentUser.IsInRole("student"))
                throw new AppException(403, "documents.upload_forbidden", "Solo el alumno puede cargar sus documentos generales.");
            return;
        }
        var query = type == "social_service"
            ? dbContext.SocialServices.Where(t => t.Id == tramiteId).Select(t => new { t.DocumentWorkflowVersion, t.Status })
            : dbContext.Internships.Where(t => t.Id == tramiteId).Select(t => new { t.DocumentWorkflowVersion, t.Status });
        var tramite = await query.SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("documents.not_found", "El trámite solicitado no existe.");
        if (tramite.Status is "released" or "cancelled" or "non_compliant")
            throw new ConflictException("documents.tramite_closed", "El trámite está cerrado y no admite cambios de evidencias.");
        if (!DocumentDefinitions.CanUpload(type, code, tramite.DocumentWorkflowVersion, currentUser))
            throw new AppException(403, "documents.upload_forbidden", "No puedes modificar este documento.");
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

    // Regla de api-conventions: Recurso ajeno responde 404 para evitar enumeración.
    private Task EnsureCanAccessStudentAsync(long studentId, CancellationToken cancellationToken) =>
        currentUser.RequireStudentAsync(dbContext, studentId, cancellationToken, true);

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
        var exists = tramiteType == "social_service"
            ? await dbContext.SocialServices.AnyAsync(t => t.Id == tramiteId && t.StudentId == studentId, cancellationToken)
            : await dbContext.Internships.AnyAsync(t => t.Id == tramiteId && t.StudentId == studentId, cancellationToken);

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
                doc.Student.ImssValidatedAt = clock.GetUtcNow();
                doc.Student.ImssValidatedBy = currentUser.Id;
            }
            else if (doc.Status == "rejected")
            {
                doc.Student.HasValidImss = false;
                doc.Student.ImssValidatedAt = null;
            }

            return;
        }

        if (!doc.TramiteId.HasValue) return;
        var today = ImssEligibilityService.Today(clock);
        if (doc.TramiteType == "social_service")
        {
            if (doc.DocumentCode is "R1" or "R2" or "R3")
            {
                var number = int.Parse(doc.DocumentCode[1..]);
                var report = await dbContext.SocialServiceBiMonthlyReports.SingleOrDefaultAsync(
                    r => r.SocialServiceId == doc.TramiteId && r.ReportNumber == number, cancellationToken);
                if (report is not null)
                {
                    report.Status = doc.Status;
                    report.OfficeObservations = doc.RejectionReason;
                    report.ApprovedBy = currentUser.Id;
                    report.ApprovedAt = doc.ReviewedAt;
                }
            }
            if (doc.Status == "approved" && doc.DocumentCode is "CP" or "CA" or "CT")
            {
                var tramite = await dbContext.SocialServices.SingleAsync(t => t.Id == doc.TramiteId, cancellationToken);
                if (doc.DocumentCode == "CP") tramite.PresentationLetterDate ??= today;
                else if (doc.DocumentCode == "CA") tramite.AcceptanceLetterDate ??= today;
                else tramite.TerminationLetterDate ??= today;
            }
        }
        else if (doc.TramiteType == "internship" && doc.Status == "approved" && doc.DocumentCode is "CA" or "CT")
        {
            var tramite = await dbContext.Internships.SingleAsync(t => t.Id == doc.TramiteId, cancellationToken);
            if (doc.DocumentCode == "CA") tramite.AcceptanceLetterDate ??= today;
            else tramite.TerminationLetterDate ??= today;
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
            d.CreatedAt, d.DocumentDate, d.Origin, d.TemplateVersion);
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
            baseResp.CreatedAt, d.DocumentDate);
    }
}
