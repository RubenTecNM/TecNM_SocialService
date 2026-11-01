using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TecNM.Api.Core.Entities;
using TecNM.Api.Core.Exceptions;
using TecNM.Api.Core.Http;
using TecNM.Api.Core.Security;
using TecNM.Api.Data;
using TecNM.Api.Modules.Catalogs;

namespace TecNM.Api.Modules.SocialService;

public sealed class SocialServiceService(
    TecNMDbContext dbContext,
    ICurrentUser currentUser,
    DocumentSeriesService documentSeriesService)
{
    private static readonly HashSet<string> TerminalStatuses =
    [
        "released",
        "cancelled",
        "non_compliant"
    ];

    private static readonly HashSet<string> ValidQualitativeGrades =
    [
        "insufficient",
        "sufficient",
        "good",
        "notable",
        "excellent"
    ];

    private static readonly HashSet<string> ValidPncCategories =
    [
        "desertion",
        "delayed_reports",
        "company_dismissal",
        "insufficient_grade",
        "invalid_documents",
        "other"
    ];

    private static readonly string[] AnexoXXIIICriteriaLabels =
    [
        "1. Cumple en tiempo y forma con las actividades encomendadas alcanzando los objetivos.",
        "2. Trabaja en equipo y se adapta a nuevas situaciones.",
        "3. Muestra liderazgo en las actividades encomendadas.",
        "4. Organiza su tiempo y trabaja de manera proactiva.",
        "5. Interpreta la realidad y se sensibiliza aportando soluciones a la problemática con la actividad complementaria.",
        "6. Realiza sugerencias innovadoras para beneficio o mejora del programa en el que participa.",
        "7. Tiene iniciativa para ayudar en las actividades encomendadas y muestra espíritu de servicio."
    ];

    private static readonly string[] AnexoXXIVCriteriaLabels =
    [
        "1. ¿Cumplí en tiempo y forma con las actividades encomendadas alcanzando los objetivos?",
        "2. ¿Trabajé en equipo y me adapté a nuevas situaciones?",
        "3. ¿Mostré liderazgo en las actividades encomendadas?",
        "4. ¿Organicé mi tiempo y trabajé de manera proactiva?",
        "5. ¿Interpreté la realidad y me sensibilicé aportando soluciones a la problemática con la actividad complementaria?",
        "6. ¿Realicé sugerencias innovadoras para beneficio o mejora del programa en el que participé?",
        "7. ¿Tuve iniciativa para ayudar en las actividades encomendadas y mostré espíritu de servicio?"
    ];

    private static readonly string[] AnexoXXVQuestionsLabels =
    [
        "1. ¿Consideras importante la realización del Servicio Social?",
        "2. ¿Consideras que las actividades que realizaste son pertinentes a los fines del Servicio Social?",
        "3. ¿Consideras que las actividades que realizaste contribuyen a tu formación integral?",
        "4. ¿Contribuiste en actividades de beneficio social comunitario?",
        "5. ¿Contribuiste en actividades de asistencia social?",
        "6. ¿Tus valores se vieron fortalecidos durante la realización del Servicio Social?",
        "7. ¿Recomendarías a otros estudiantes realizar su Servicio Social en la dependencia donde lo realizaste?",
        "8. ¿El trato que recibiste en la dependencia fue respetuoso y adecuado?"
    ];

    public async Task<PagedResponse<SocialServiceListItemResponse>> GetPagedAsync(
        int page,
        int pageSize,
        long? periodId,
        string? status,
        long? careerId,
        long? companyId,
        long? programId,
        string? search,
        string? sort,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedSize = Math.Clamp(pageSize, 1, 100);

        var query = BuildBaseQuery().AsNoTracking();

        if (periodId.HasValue)
        {
            query = query.Where(s => s.PeriodId == periodId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normStatus = status.Trim().ToLowerInvariant();
            query = query.Where(s => s.Status == normStatus);
        }

        if (careerId.HasValue)
        {
            query = query.Where(s => s.Student != null && s.Student.CareerId == careerId.Value);
        }

        if (companyId.HasValue)
        {
            query = query.Where(s => s.CompanyId == companyId.Value);
        }

        if (programId.HasValue)
        {
            query = query.Where(s => s.ProgramId == programId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            if (term.Length > 100)
            {
                term = term[..100];
            }

            var pattern = $"%{term}%";
            query = query.Where(s =>
                (s.Student != null && (
                    EF.Functions.ILike(s.Student.ControlNumber, pattern) ||
                    EF.Functions.ILike(s.Student.FirstName, pattern) ||
                    EF.Functions.ILike(s.Student.FirstSurname, pattern) ||
                    (s.Student.SecondSurname != null && EF.Functions.ILike(s.Student.SecondSurname, pattern)))) ||
                (s.Company != null && EF.Functions.ILike(s.Company.Name, pattern)));
        }

        query = (sort?.Trim().ToLowerInvariant()) switch
        {
            "controlnumber" => query.OrderBy(s => s.Student!.ControlNumber).ThenByDescending(s => s.Id),
            "-controlnumber" => query.OrderByDescending(s => s.Student!.ControlNumber).ThenByDescending(s => s.Id),
            "startdate" => query.OrderBy(s => s.StartDate).ThenByDescending(s => s.Id),
            "-startdate" => query.OrderByDescending(s => s.StartDate).ThenByDescending(s => s.Id),
            "status" => query.OrderBy(s => s.Status).ThenByDescending(s => s.Id),
            "-status" => query.OrderByDescending(s => s.Status).ThenByDescending(s => s.Id),
            _ => query.OrderByDescending(s => s.UpdatedAt).ThenByDescending(s => s.Id)
        };

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalItems / (double)normalizedSize);

        var items = await query
            .Skip((normalizedPage - 1) * normalizedSize)
            .Take(normalizedSize)
            .ToListAsync(cancellationToken);

        var serviceIds = items.Select(i => i.Id).ToList();
        var docsByService = await LoadDocumentsByServiceIdsAsync(serviceIds, cancellationToken);

        var responses = items
            .Select(item =>
            {
                docsByService.TryGetValue(item.Id, out var docs);
                return ToListItemResponse(item, docs ?? []);
            })
            .ToList();

        return new PagedResponse<SocialServiceListItemResponse>(
            responses,
            normalizedPage,
            normalizedSize,
            totalItems,
            totalPages);
    }

    public async Task<IReadOnlyList<SocialServiceListItemResponse>> GetMyListAsync(
        CancellationToken cancellationToken = default)
    {
        var studentId = await ResolveCurrentStudentIdAsync(cancellationToken);

        var items = await BuildBaseQuery()
            .AsNoTracking()
            .Where(s => s.StudentId == studentId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        var serviceIds = items.Select(i => i.Id).ToList();
        var docsByService = await LoadDocumentsByServiceIdsAsync(serviceIds, cancellationToken);

        return items
            .Select(item =>
            {
                docsByService.TryGetValue(item.Id, out var docs);
                return ToListItemResponse(item, docs ?? []);
            })
            .ToList();
    }

    public async Task<SocialServiceDetailResponse> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await BuildDetailQuery()
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException("social_services.not_found", $"Trámite de Servicio Social #{id} no encontrado.");

        EnsureCanViewTramite(entity);

        var docs = await dbContext.ExpedientDocuments
            .AsNoTracking()
            .Where(d => d.StudentId == entity.StudentId
                        && d.TramiteType == "social_service"
                        && d.TramiteId == entity.Id
                        && d.Status != "superseded")
            .ToListAsync(cancellationToken);

        return ToDetailResponse(entity, docs);
    }

    public async Task<SocialServiceDetailResponse> CreateAsync(
        CreateSocialServiceRequest request,
        CancellationToken cancellationToken = default)
    {
        long targetStudentId;
        if (IsSocialServiceStaff())
        {
            if (!request.StudentId.HasValue || request.StudentId.Value <= 0)
            {
                throw new ValidationException(new Dictionary<string, string[]>
                {
                    ["studentId"] = ["El identificador del estudiante es obligatorio."]
                });
            }

            targetStudentId = request.StudentId.Value;
        }
        else
        {
            targetStudentId = await ResolveCurrentStudentIdAsync(cancellationToken);
        }

        var student = await dbContext.Students
            .SingleOrDefaultAsync(s => s.Id == targetStudentId, cancellationToken)
            ?? throw new NotFoundException("students.not_found", $"Estudiante con id {targetStudentId} no encontrado.");

        if (!student.IsEnrolled)
        {
            throw new BusinessRuleException(
                "social_services.student_not_eligible",
                "El estudiante no se encuentra inscrito o en condiciones de realizar el Servicio Social.");
        }

        var hasActiveOrReleased = await dbContext.SocialServices
            .AnyAsync(s => s.StudentId == targetStudentId && s.Status != "cancelled" && s.Status != "non_compliant", cancellationToken);

        if (hasActiveOrReleased)
        {
            throw new BusinessRuleException(
                "social_services.student_not_eligible",
                "El estudiante ya cuenta con un trámite de Servicio Social activo o liberado.");
        }

        var (_, _, _, _, normalizedModality) = await ValidateReferencesAndApplicationAsync(
            request.PeriodId,
            request.ProgramId,
            request.CompanyId,
            request.SupervisorId,
            request.AgreementId,
            request.Modality,
            request.StartDate,
            request.EndDate,
            request.TargetCommunity,
            request.Application,
            cancellationToken);

        var entity = new Core.Entities.SocialService
        {
            StudentId = student.Id,
            PeriodId = request.PeriodId,
            ProgramId = request.ProgramId,
            CompanyId = request.CompanyId,
            SupervisorId = request.SupervisorId,
            AgreementId = request.AgreementId,
            SemesterEnrolled = student.CurrentSemester,
            Modality = normalizedModality,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            RequiredHours = 480,
            AccumulatedHours = 0,
            Status = "requested",
            TargetCommunity = string.IsNullOrWhiteSpace(request.TargetCommunity) ? null : request.TargetCommunity.Trim()
        };

        // Sincronizar teléfono del alumno si lo actualizó en el formulario de apertura
        student.PhoneNumber = request.Application.StudentPhone.Trim();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        dbContext.SocialServices.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        var application = new SocialServiceApplication
        {
            SocialServiceId = entity.Id,
            StudentAddress = request.Application.StudentAddress.Trim(),
            StudentPhone = request.Application.StudentPhone.Trim(),
            PeriodType = request.Application.PeriodType.Trim().ToLowerInvariant(),
            CurrentSemester = student.CurrentSemester,
            DependencyName = request.Application.DependencyName.Trim(),
            DependencyHeadName = request.Application.DependencyHeadName.Trim(),
            DependencyHeadPosition = request.Application.DependencyHeadPosition.Trim(),
            ActivitiesOutline = request.Application.ActivitiesOutline.Trim(),
            IsAcceptedByOffice = null
        };

        dbContext.SocialServiceApplications.Add(application);
        AddStatusLog(entity.Id, null, "requested", "Apertura de solicitud de Servicio Social (Anexo XVIII)");

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<SocialServiceDetailResponse> UpdateAsync(
        long id,
        UpdateSocialServiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.SocialServices
            .Include(s => s.Application)
            .Include(s => s.Student)
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException("social_services.not_found", $"Trámite de Servicio Social #{id} no encontrado.");

        EnsureCanModifyTramite(entity, allowStudentOnlyWhenRequested: true);

        if (TerminalStatuses.Contains(entity.Status))
        {
            throw new ConflictException(
                "social_services.invalid_transition",
                $"No se puede editar un trámite en estado terminal ('{entity.Status}').");
        }

        var (_, _, _, _, normalizedModality) = await ValidateReferencesAndApplicationAsync(
            request.PeriodId,
            request.ProgramId,
            request.CompanyId,
            request.SupervisorId,
            request.AgreementId,
            request.Modality,
            request.StartDate,
            request.EndDate,
            request.TargetCommunity,
            request.Application,
            cancellationToken);

        entity.PeriodId = request.PeriodId;
        entity.ProgramId = request.ProgramId;
        entity.CompanyId = request.CompanyId;
        entity.SupervisorId = request.SupervisorId;
        entity.AgreementId = request.AgreementId;
        entity.Modality = normalizedModality;
        entity.StartDate = request.StartDate;
        entity.EndDate = request.EndDate;
        entity.TargetCommunity = string.IsNullOrWhiteSpace(request.TargetCommunity) ? null : request.TargetCommunity.Trim();

        if (entity.Application is not null)
        {
            entity.Application.StudentAddress = request.Application.StudentAddress.Trim();
            entity.Application.StudentPhone = request.Application.StudentPhone.Trim();
            entity.Application.PeriodType = request.Application.PeriodType.Trim().ToLowerInvariant();
            entity.Application.DependencyName = request.Application.DependencyName.Trim();
            entity.Application.DependencyHeadName = request.Application.DependencyHeadName.Trim();
            entity.Application.DependencyHeadPosition = request.Application.DependencyHeadPosition.Trim();
            entity.Application.ActivitiesOutline = request.Application.ActivitiesOutline.Trim();

            if (entity.Application.IsAcceptedByOffice == false)
            {
                entity.Application.IsAcceptedByOffice = null;
                entity.Application.RejectionReason = null;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<SocialServiceDetailResponse> ReviewApplicationAsync(
        long id,
        ReviewApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.SocialServices
            .Include(s => s.Application)
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException("social_services.not_found", $"Trámite de Servicio Social #{id} no encontrado.");

        if (entity.Status != "requested")
        {
            throw new ConflictException(
                "social_services.invalid_transition",
                $"La solicitud solo puede dictaminarse en estado 'requested' (actual: '{entity.Status}').");
        }

        if (!request.IsAccepted && string.IsNullOrWhiteSpace(request.RejectionReason))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["rejectionReason"] = ["El motivo de rechazo es obligatorio cuando no se acepta la solicitud."]
            });
        }

        if (entity.Application is not null)
        {
            entity.Application.IsAcceptedByOffice = request.IsAccepted;
            entity.Application.RejectionReason = request.IsAccepted ? null : request.RejectionReason?.Trim();
            entity.Application.OfficeObservations = string.IsNullOrWhiteSpace(request.OfficeObservations)
                ? null
                : request.OfficeObservations.Trim();
            entity.Application.ReviewedBy = currentUser.Id;
            entity.Application.ReviewedAt = DateTimeOffset.UtcNow;
        }

        if (request.IsAccepted)
        {
            var previousStatus = entity.Status;
            entity.Status = "approved";
            entity.PresentationLetterDate ??= DateOnly.FromDateTime(DateTime.UtcNow);

            if (string.IsNullOrWhiteSpace(entity.PresentationLetterFolio))
            {
                entity.PresentationLetterFolio = await documentSeriesService.GenerateNextFolioAsync(
                    "social_service_presentation_letter",
                    cancellationToken);
            }

            AddStatusLog(entity.Id, previousStatus, "approved", "Solicitud Anexo XVIII aprobada y Carta de Presentación emitida");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<SocialServiceDetailResponse> RegisterPresentationLetterAsync(
        long id,
        PresentationLetterRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.SocialServices
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException("social_services.not_found", $"Trámite de Servicio Social #{id} no encontrado.");

        if (TerminalStatuses.Contains(entity.Status))
        {
            throw new ConflictException("social_services.invalid_transition", "El trámite se encuentra en un estado terminal.");
        }

        entity.PresentationLetterDate = request.PresentationLetterDate;
        if (string.IsNullOrWhiteSpace(entity.PresentationLetterFolio))
        {
            entity.PresentationLetterFolio = await documentSeriesService.GenerateNextFolioAsync(
                "social_service_presentation_letter",
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<SocialServiceDetailResponse> RegisterAcceptanceAsync(
        long id,
        AcceptanceLetterRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.SocialServices
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException("social_services.not_found", $"Trámite de Servicio Social #{id} no encontrado.");

        if (entity.Status != "approved")
        {
            throw new ConflictException(
                "social_services.invalid_transition",
                $"Se requiere estado 'approved' para registrar la Carta de Aceptación (actual: '{entity.Status}').");
        }

        var hasApprovedCa = await HasApprovedDocumentAsync(entity.StudentId, entity.Id, "CA", cancellationToken);
        if (!hasApprovedCa)
        {
            throw new BusinessRuleException(
                "social_services.documents_missing",
                "Debe aprobarse primero el documento PDF de la Carta de Aceptación ('CA') firmada y sellada.");
        }

        var previous = entity.Status;
        entity.AcceptanceLetterDate = request.AcceptanceLetterDate;
        entity.Status = "accepted";
        AddStatusLog(entity.Id, previous, "accepted", "Carta de Aceptación validada");

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<SocialServiceDetailResponse> UpsertCommitmentAsync(
        long id,
        CommitmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.SocialServices
            .Include(s => s.Commitment)
            .Include(s => s.Student)
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException("social_services.not_found", $"Trámite de Servicio Social #{id} no encontrado.");

        EnsureCanModifyTramite(entity, allowStudentOnlyWhenRequested: false);

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.StudentAddress))
        {
            errors["studentAddress"] = ["El domicilio del estudiante es obligatorio."];
        }

        if (string.IsNullOrWhiteSpace(request.StudentPhone) || request.StudentPhone.Trim().Length > 20)
        {
            errors["studentPhone"] = ["El teléfono es obligatorio y no debe exceder 20 caracteres."];
        }

        if (string.IsNullOrWhiteSpace(request.DependencyAddress))
        {
            errors["dependencyAddress"] = ["El domicilio de la dependencia es obligatorio."];
        }

        var city = string.IsNullOrWhiteSpace(request.City) ? "Monclova" : request.City.Trim();
        if (city.Length > 100)
        {
            errors["city"] = ["La ciudad no puede exceder 100 caracteres."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        if (entity.Commitment is null)
        {
            entity.Commitment = new SocialServiceCommitment
            {
                SocialServiceId = entity.Id,
                StudentControlNumber = entity.Student?.ControlNumber ?? string.Empty,
                StudentAddress = request.StudentAddress.Trim(),
                StudentPhone = request.StudentPhone.Trim(),
                DependencyAddress = request.DependencyAddress.Trim(),
                CommitmentDate = request.CommitmentDate,
                City = city,
                IsSignedByStudent = true,
                SignedAt = DateTimeOffset.UtcNow
            };
            dbContext.SocialServiceCommitments.Add(entity.Commitment);
        }
        else
        {
            entity.Commitment.StudentAddress = request.StudentAddress.Trim();
            entity.Commitment.StudentPhone = request.StudentPhone.Trim();
            entity.Commitment.DependencyAddress = request.DependencyAddress.Trim();
            entity.Commitment.CommitmentDate = request.CommitmentDate;
            entity.Commitment.City = city;
            entity.Commitment.SignedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<SocialServiceDetailResponse> ActivateAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.SocialServices
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException("social_services.not_found", $"Trámite de Servicio Social #{id} no encontrado.");

        if (entity.Status is not ("accepted" or "approved"))
        {
            throw new ConflictException(
                "social_services.invalid_transition",
                $"Se requiere estado 'accepted' o 'approved' para activar el Servicio Social (actual: '{entity.Status}').");
        }

        var hasApprovedCc = await HasApprovedDocumentAsync(entity.StudentId, entity.Id, "CC", cancellationToken);
        if (!hasApprovedCc)
        {
            throw new BusinessRuleException(
                "social_services.documents_missing",
                "Se requiere que el PDF de la Carta Compromiso ('CC') firmada esté aprobado.");
        }

        var previous = entity.Status;
        entity.Status = "active";
        AddStatusLog(entity.Id, previous, "active", "Inicio formal de Servicio Social (Carta Compromiso validada)");

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<BiMonthlyReportResponse> UpsertReportAsync(
        long id,
        int reportNumber,
        ReportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (reportNumber is < 1 or > 3)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["reportNumber"] = ["El número de reporte bimestral debe ser 1, 2 o 3."]
            });
        }

        var entity = await dbContext.SocialServices
            .Include(s => s.Student)
            .Include(s => s.Reports)
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException("social_services.not_found", $"Trámite de Servicio Social #{id} no encontrado.");

        EnsureCanModifyTramite(entity, allowStudentOnlyWhenRequested: false);

        var errors = new Dictionary<string, string[]>();
        if (request.PeriodEndDate < request.PeriodStartDate)
        {
            errors["periodEndDate"] = ["La fecha final del periodo debe ser mayor o igual a la fecha inicial."];
        }

        if (string.IsNullOrWhiteSpace(request.ActivitiesSummary))
        {
            errors["activitiesSummary"] = ["El resumen de actividades realizadas es obligatorio para imprimir el formato."];
        }

        var reportedHours = request.ReportedHours <= 0 ? 160 : request.ReportedHours;

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        var report = entity.Reports.SingleOrDefault(r => r.ReportNumber == reportNumber);
        if (report is null)
        {
            report = new SocialServiceBiMonthlyReport
            {
                SocialServiceId = entity.Id,
                ReportNumber = reportNumber,
                PeriodStartDate = request.PeriodStartDate,
                PeriodEndDate = request.PeriodEndDate,
                ActivitiesSummary = request.ActivitiesSummary.Trim(),
                ReportedHours = reportedHours,
                AccumulatedHours = reportedHours,
                EvaluatorSupervisorId = request.EvaluatorSupervisorId ?? entity.SupervisorId,
                Status = "submitted"
            };
            entity.Reports.Add(report);
            dbContext.SocialServiceBiMonthlyReports.Add(report);
        }
        else
        {
            report.PeriodStartDate = request.PeriodStartDate;
            report.PeriodEndDate = request.PeriodEndDate;
            report.ActivitiesSummary = request.ActivitiesSummary.Trim();
            report.ReportedHours = reportedHours;
            report.EvaluatorSupervisorId = request.EvaluatorSupervisorId ?? entity.SupervisorId;
        }

        // Recalcular horas acumuladas informativas para impresión de Anexo XXII
        var runningHours = 0;
        foreach (var r in entity.Reports.OrderBy(x => x.ReportNumber))
        {
            runningHours += r.ReportedHours;
            r.AccumulatedHours = runningHours;
        }

        entity.AccumulatedHours = runningHours;

        await dbContext.SaveChangesAsync(cancellationToken);

        var saved = await dbContext.SocialServiceBiMonthlyReports
            .Include(r => r.Approver)
            .AsNoTracking()
            .SingleAsync(r => r.Id == report.Id, cancellationToken);

        return ToReportResponse(saved);
    }

    public async Task<BiMonthlyReportResponse> ReviewReportAsync(
        long id,
        long reportId,
        ReviewReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var report = await dbContext.SocialServiceBiMonthlyReports
            .Include(r => r.Approver)
            .SingleOrDefaultAsync(r => r.Id == reportId && r.SocialServiceId == id, cancellationToken)
            ?? throw new NotFoundException("social_services.report_not_found", "Reporte bimestral no encontrado.");

        var normStatus = request.Status?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normStatus is not ("approved" or "rejected"))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["status"] = ["El estado debe ser 'approved' o 'rejected'."]
            });
        }

        report.Status = normStatus;
        report.OfficeObservations = string.IsNullOrWhiteSpace(request.OfficeObservations)
            ? null
            : request.OfficeObservations.Trim();
        report.ApprovedBy = currentUser.Id;
        report.ApprovedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        var updated = await dbContext.SocialServiceBiMonthlyReports
            .Include(r => r.Approver)
            .AsNoTracking()
            .SingleAsync(r => r.Id == report.Id, cancellationToken);

        return ToReportResponse(updated);
    }

    public async Task<SocialServiceEvaluationResponse> UpsertEvaluationAsync(
        long id,
        string evaluatorType,
        string bimonthlyPeriod,
        EvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        var normType = evaluatorType?.Trim().ToLowerInvariant() ?? string.Empty;
        var normPeriod = bimonthlyPeriod?.Trim().ToLowerInvariant() ?? string.Empty;

        var errors = new Dictionary<string, string[]>();
        if (normType is not ("external_supervisor" or "student_self"))
        {
            errors["evaluatorType"] = ["El tipo de evaluador debe ser 'external_supervisor' (Anexo XXIII) o 'student_self' (Anexo XXIV)."];
        }

        if (normPeriod is not ("1" or "2" or "3" or "final"))
        {
            errors["bimonthlyPeriod"] = ["El periodo bimestral debe ser '1', '2', '3' o 'final'."];
        }

        var criteria = request.Criteria is { Count: 7 }
            ? request.Criteria.Select(c => c.Trim().ToLowerInvariant()).ToArray()
            : ["excellent", "excellent", "excellent", "excellent", "excellent", "excellent", "excellent"];

        if (criteria.Any(c => !ValidQualitativeGrades.Contains(c)))
        {
            errors["criteria"] = [$"Todos los 7 criterios deben tener un nivel válido: {string.Join(", ", ValidQualitativeGrades)}."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        var entity = await dbContext.SocialServices
            .Include(s => s.Student)
            .Include(s => s.Evaluations)
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException("social_services.not_found", $"Trámite de Servicio Social #{id} no encontrado.");

        EnsureCanModifyTramite(entity, allowStudentOnlyWhenRequested: false);

        var eval = entity.Evaluations.SingleOrDefault(
            e => e.EvaluatorType == normType && e.BimonthlyPeriod == normPeriod);

        if (eval is null)
        {
            eval = new SocialServiceEvaluation
            {
                SocialServiceId = entity.Id,
                EvaluatorType = normType,
                BimonthlyPeriod = normPeriod,
                Criterion1 = criteria[0],
                Criterion2 = criteria[1],
                Criterion3 = criteria[2],
                Criterion4 = criteria[3],
                Criterion5 = criteria[4],
                Criterion6 = criteria[5],
                Criterion7 = criteria[6],
                EvaluatorSupervisorId = request.EvaluatorSupervisorId ?? entity.SupervisorId,
                Observations = string.IsNullOrWhiteSpace(request.Observations) ? null : request.Observations.Trim()
            };
            dbContext.SocialServiceEvaluations.Add(eval);
        }
        else
        {
            eval.Criterion1 = criteria[0];
            eval.Criterion2 = criteria[1];
            eval.Criterion3 = criteria[2];
            eval.Criterion4 = criteria[3];
            eval.Criterion5 = criteria[4];
            eval.Criterion6 = criteria[5];
            eval.Criterion7 = criteria[6];
            eval.EvaluatorSupervisorId = request.EvaluatorSupervisorId ?? entity.SupervisorId;
            eval.Observations = string.IsNullOrWhiteSpace(request.Observations) ? null : request.Observations.Trim();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToEvaluationResponse(eval);
    }

    public async Task<ActivityAssessmentResponse> UpsertActivityAssessmentAsync(
        long id,
        ActivityAssessmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var questions = request.Questions is { Count: 8 }
            ? request.Questions.Select(q => q.Trim().ToLowerInvariant()).ToArray()
            : ["excellent", "excellent", "excellent", "excellent", "excellent", "excellent", "excellent", "excellent"];

        if (questions.Any(q => !ValidQualitativeGrades.Contains(q)))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["questions"] = [$"Las 8 respuestas deben usar la escala oficial: {string.Join(", ", ValidQualitativeGrades)}."]
            });
        }

        var entity = await dbContext.SocialServices
            .Include(s => s.Student)
            .Include(s => s.ActivityAssessment)
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException("social_services.not_found", $"Trámite de Servicio Social #{id} no encontrado.");

        EnsureCanModifyTramite(entity, allowStudentOnlyWhenRequested: false);

        if (entity.ActivityAssessment is null)
        {
            entity.ActivityAssessment = new SocialServiceActivityAssessment
            {
                SocialServiceId = entity.Id,
                Question1 = questions[0],
                Question2 = questions[1],
                Question3 = questions[2],
                Question4 = questions[3],
                Question5 = questions[4],
                Question6 = questions[5],
                Question7 = questions[6],
                Question8 = questions[7],
                Observations = string.IsNullOrWhiteSpace(request.Observations) ? null : request.Observations.Trim()
            };
            dbContext.SocialServiceActivityAssessments.Add(entity.ActivityAssessment);
        }
        else
        {
            entity.ActivityAssessment.Question1 = questions[0];
            entity.ActivityAssessment.Question2 = questions[1];
            entity.ActivityAssessment.Question3 = questions[2];
            entity.ActivityAssessment.Question4 = questions[3];
            entity.ActivityAssessment.Question5 = questions[4];
            entity.ActivityAssessment.Question6 = questions[5];
            entity.ActivityAssessment.Question7 = questions[6];
            entity.ActivityAssessment.Question8 = questions[7];
            entity.ActivityAssessment.Observations = string.IsNullOrWhiteSpace(request.Observations) ? null : request.Observations.Trim();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToActivityAssessmentResponse(entity.ActivityAssessment);
    }

    public async Task<SocialServiceDetailResponse> UpdateImpactAsync(
        long id,
        UpdateImpactRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.BeneficiariesCount < 0)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["beneficiariesCount"] = ["El número de beneficiarios no puede ser negativo."]
            });
        }

        var entity = await dbContext.SocialServices
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException("social_services.not_found", $"Trámite de Servicio Social #{id} no encontrado.");

        entity.BeneficiariesCount = request.BeneficiariesCount;
        entity.HasInclusionActivities = request.HasInclusionActivities;
        entity.TargetCommunity = string.IsNullOrWhiteSpace(request.TargetCommunity) ? null : request.TargetCommunity.Trim();
        entity.Observations = string.IsNullOrWhiteSpace(request.Observations) ? null : request.Observations.Trim();

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<SocialServiceDetailResponse> CompleteAsync(
        long id,
        CompleteSocialServiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.SocialServices
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException("social_services.not_found", $"Trámite de Servicio Social #{id} no encontrado.");

        if (entity.Status is not ("active" or "accepted" or "approved"))
        {
            throw new ConflictException(
                "social_services.invalid_transition",
                $"No se puede marcar como concluido desde el estado '{entity.Status}'.");
        }

        // Regla: Progreso guiado por documentos (sin bloqueo por horas)
        var approvedCodes = await GetApprovedDocumentCodesAsync(entity.StudentId, entity.Id, cancellationToken);
        var requiredDocs = new[] { "R1", "R2", "R3", "CT" };
        var missing = requiredDocs.Where(code => !approvedCodes.Contains(code)).ToList();

        if (missing.Count > 0)
        {
            throw new BusinessRuleException(
                "social_services.documents_missing",
                $"Faltan documentos firmados y sellados por aprobar en el expediente: {string.Join(", ", missing)}.");
        }

        var previous = entity.Status;
        entity.TerminationLetterDate = request.TerminationLetterDate;
        entity.Status = "completed_pending_review";
        AddStatusLog(entity.Id, previous, "completed_pending_review", "Expediente documental concluido (Carta de Terminación validada)");

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<SocialServiceDetailResponse> ReleaseAsync(
        long id,
        ReleaseSocialServiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.SocialServices
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException("social_services.not_found", $"Trámite de Servicio Social #{id} no encontrado.");

        if (entity.Status is not ("completed_pending_review" or "active"))
        {
            throw new ConflictException(
                "social_services.invalid_transition",
                $"El trámite debe estar en 'completed_pending_review' o 'active' para emitir la liberación (actual: '{entity.Status}').");
        }

        var approvedCodes = await GetApprovedDocumentCodesAsync(entity.StudentId, entity.Id, cancellationToken);
        var requiredDocs = new[] { "CA", "CC", "R1", "R2", "R3", "CT" };
        var missing = requiredDocs.Where(code => !approvedCodes.Contains(code)).ToList();

        if (missing.Count > 0)
        {
            throw new BusinessRuleException(
                "social_services.documents_missing",
                $"No se puede liberar el Servicio Social porque faltan documentos aprobados: {string.Join(", ", missing)}.");
        }

        var gradeLevel = string.IsNullOrWhiteSpace(request.FinalGradeLevel)
            ? "excellent"
            : request.FinalGradeLevel.Trim().ToLowerInvariant();

        if (!ValidQualitativeGrades.Contains(gradeLevel))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["finalGradeLevel"] = [$"El nivel de desempeño debe ser uno de: {string.Join(", ", ValidQualitativeGrades)}."]
            });
        }

        var folio = await documentSeriesService.GenerateNextFolioAsync(
            "social_service_release_certificate",
            cancellationToken);

        var previous = entity.Status;
        entity.Status = "released";
        entity.ReleaseCertificateFolio = folio;
        entity.ReleasedAt = DateTimeOffset.UtcNow;
        entity.OriginalTerminationLetterReceived = request.OriginalTerminationLetterReceived;
        entity.FinalGradeLevel = gradeLevel;
        entity.FinalScore = request.FinalScore;
        entity.TerminationLetterDate ??= DateOnly.FromDateTime(DateTime.UtcNow);

        AddStatusLog(entity.Id, previous, "released", $"Constancia de Terminación emitida con folio {folio}");

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<SocialServiceDetailResponse> SendSchoolServicesActAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.SocialServices
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException("social_services.not_found", $"Trámite de Servicio Social #{id} no encontrado.");

        if (entity.Status != "released")
        {
            throw new ConflictException(
                "social_services.invalid_transition",
                "Únicamente los trámites liberados ('released') pueden enviarse en acta a Servicios Escolares.");
        }

        if (entity.SchoolServicesActSent)
        {
            throw new ConflictException(
                "social_services.act_already_sent",
                $"El acta a Servicios Escolares ya fue enviada previamente con el folio '{entity.SchoolServicesActFolio}'.");
        }

        var folio = await documentSeriesService.GenerateNextFolioAsync("school_services_act", cancellationToken);
        entity.SchoolServicesActSent = true;
        entity.SchoolServicesActSentAt = DateTimeOffset.UtcNow;
        entity.SchoolServicesActFolio = folio;

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<SocialServiceDetailResponse> CancelAsync(
        long id,
        CancelTramiteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["reason"] = ["El motivo de cancelación es obligatorio."]
            });
        }

        var entity = await dbContext.SocialServices
            .Include(s => s.Student)
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException("social_services.not_found", $"Trámite de Servicio Social #{id} no encontrado.");

        EnsureCanModifyTramite(entity, allowStudentOnlyWhenRequested: true);

        if (TerminalStatuses.Contains(entity.Status))
        {
            throw new ConflictException(
                "social_services.invalid_transition",
                $"No se puede cancelar un trámite que ya está en estado terminal ('{entity.Status}').");
        }

        var previous = entity.Status;
        entity.Status = "cancelled";
        entity.CancellationReason = request.Reason.Trim();
        AddStatusLog(entity.Id, previous, "cancelled", entity.CancellationReason);

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<SocialServiceDetailResponse> MarkNonComplianceAsync(
        long id,
        MarkNonComplianceRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = request.Category?.Trim().ToLowerInvariant() ?? string.Empty;
        var errors = new Dictionary<string, string[]>();

        if (!ValidPncCategories.Contains(category))
        {
            errors["category"] = [$"Categoría PNC inválida. Opciones: {string.Join(", ", ValidPncCategories)}."];
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            errors["description"] = ["La descripción de la incidencia de Producto No Conforme es obligatoria."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        var entity = await dbContext.SocialServices
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException("social_services.not_found", $"Trámite de Servicio Social #{id} no encontrado.");

        if (TerminalStatuses.Contains(entity.Status))
        {
            throw new ConflictException(
                "social_services.invalid_transition",
                $"No se puede marcar como No Conforme un trámite en estado terminal ('{entity.Status}').");
        }

        var previous = entity.Status;
        entity.Status = "non_compliant";
        entity.CancellationReason = request.Description.Trim();

        var incidentCode = $"PNC-SS-{DateTime.UtcNow.Year}-{entity.Id:D4}-{Random.Shared.Next(10, 99)}";
        var pnc = new NonConformance
        {
            TramiteType = "social_service",
            TramiteId = entity.Id,
            StudentId = entity.StudentId,
            PeriodId = entity.PeriodId,
            IncidentCode = incidentCode,
            Category = category,
            Description = request.Description.Trim(),
            Status = "open"
        };

        dbContext.NonConformances.Add(pnc);
        AddStatusLog(entity.Id, previous, "non_compliant", $"PNC {incidentCode}: {request.Description.Trim()}");

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<StatusLogItemResponse>> GetHistoryAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.SocialServices
            .Include(s => s.Student)
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException("social_services.not_found", $"Trámite de Servicio Social #{id} no encontrado.");

        EnsureCanViewTramite(entity);

        var logs = await dbContext.TramiteStatusLogs
            .Include(l => l.ChangedByUser)
            .AsNoTracking()
            .Where(l => l.TramiteType == "social_service" && l.TramiteId == id)
            .OrderByDescending(l => l.CreatedAt)
            .ThenByDescending(l => l.Id)
            .ToListAsync(cancellationToken);

        return logs
            .Select(l => new StatusLogItemResponse(
                l.Id,
                l.PreviousStatus,
                l.NewStatus,
                l.Reason,
                l.ChangedByUser is null
                    ? "Sistema"
                    : string.Join(" ", new[] { l.ChangedByUser.FirstName, l.ChangedByUser.FirstSurname, l.ChangedByUser.SecondSurname }
                        .Where(s => !string.IsNullOrWhiteSpace(s))),
                l.CreatedAt))
            .ToList();
    }

    public async Task<Dictionary<string, object?>> GetPrintDataAsync(
        long id,
        string document,
        CancellationToken cancellationToken = default)
    {
        var entity = await BuildDetailQuery()
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException("social_services.not_found", $"Trámite de Servicio Social #{id} no encontrado.");

        EnsureCanViewTramite(entity);

        var normDoc = document.Trim().ToLowerInvariant();
        var authorities = await dbContext.InstitutionalAuthorities
            .AsNoTracking()
            .Where(a => a.IsActive)
            .ToDictionaryAsync(a => a.RoleKey, cancellationToken);

        var student = entity.Student!;
        var studentFullName = string.Join(" ", new[] { student.FirstName, student.FirstSurname, student.SecondSurname }
            .Where(s => !string.IsNullOrWhiteSpace(s)));

        var data = new Dictionary<string, object?>
        {
            ["documentType"] = normDoc,
            ["tramiteId"] = entity.Id,
            ["generatedAt"] = DateTimeOffset.UtcNow,
            ["studentControlNumber"] = student.ControlNumber,
            ["studentFullName"] = studentFullName,
            ["studentFirstName"] = student.FirstName,
            ["studentFirstSurname"] = student.FirstSurname,
            ["studentSecondSurname"] = student.SecondSurname,
            ["studentGender"] = student.Gender,
            ["studentCurp"] = student.Curp,
            ["studentEmail"] = student.InstitutionalEmail ?? student.PersonalEmail,
            ["studentPhone"] = entity.Application?.StudentPhone ?? student.PhoneNumber,
            ["studentAddress"] = entity.Application?.StudentAddress ?? entity.Commitment?.StudentAddress,
            ["studentSemester"] = entity.SemesterEnrolled,
            ["studentAccumulatedCredits"] = student.CompletedCredits,
            ["studentCreditsPercentage"] = student.CurriculumProgressPercentage,
            ["careerCode"] = student.Career?.Code,
            ["careerName"] = student.Career?.Name,
            ["periodCode"] = entity.Period?.Code,
            ["periodName"] = entity.Period?.Name,
            ["programCode"] = entity.Program?.Code,
            ["programName"] = entity.Program?.Name,
            ["programCategory"] = entity.Program?.Category,
            ["modality"] = entity.Modality,
            ["companyName"] = entity.Company?.Name,
            ["companyCommercialName"] = entity.Company?.CommercialName,
            ["companyAddress"] = entity.Company?.Address,
            ["companyCity"] = entity.Company?.City ?? "Monclova",
            ["companyState"] = entity.Company?.State ?? "Coahuila",
            ["isTecnmInternal"] = entity.Company?.IsTecnmInternal ?? false,
            ["supervisorFullName"] = entity.Supervisor?.FullName,
            ["supervisorPosition"] = entity.Supervisor?.Position,
            ["supervisorDepartment"] = entity.Supervisor?.Department,
            ["startDate"] = entity.StartDate,
            ["endDate"] = entity.EndDate,
            ["requiredHours"] = entity.RequiredHours,
            ["accumulatedHours"] = entity.AccumulatedHours,
            ["presentationLetterFolio"] = entity.PresentationLetterFolio,
            ["presentationLetterDate"] = entity.PresentationLetterDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            ["acceptanceLetterDate"] = entity.AcceptanceLetterDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            ["terminationLetterDate"] = entity.TerminationLetterDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            ["releaseCertificateFolio"] = entity.ReleaseCertificateFolio,
            ["authorities"] = authorities.ToDictionary(
                kvp => kvp.Key,
                kvp => new
                {
                    kvp.Value.Title,
                    kvp.Value.FullName,
                    kvp.Value.Position,
                    kvp.Value.SignatureInitials
                })
        };

        switch (normDoc)
        {
            case "application":
                data["anexo"] = "ANEXO XVIII — Solicitud de Servicio Social";
                data["periodType"] = entity.Application?.PeriodType ?? "february_july";
                data["dependencyName"] = entity.Application?.DependencyName ?? entity.Company?.Name;
                data["dependencyHeadName"] = entity.Application?.DependencyHeadName ?? entity.Supervisor?.FullName;
                data["dependencyHeadPosition"] = entity.Application?.DependencyHeadPosition ?? entity.Supervisor?.Position;
                data["activitiesOutline"] = entity.Application?.ActivitiesOutline ?? string.Empty;
                data["isAcceptedByOffice"] = entity.Application?.IsAcceptedByOffice;
                data["officeObservations"] = entity.Application?.OfficeObservations;
                break;

            case "presentation_letter":
                data["anexo"] = "Carta de Presentación de Servicio Social";
                data["folio"] = entity.PresentationLetterFolio ?? "PENDIENTE";
                data["issueDate"] = entity.PresentationLetterDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
                break;

            case "acceptance_letter":
                if (entity.Modality != "internal" && entity.Company?.IsTecnmInternal != true)
                {
                    throw new ConflictException(
                        "social_services.not_available",
                        "La plantilla automática de Carta de Aceptación solo está disponible cuando el Servicio Social se realiza de manera local en el TecNM ('internal').");
                }

                data["anexo"] = "Carta de Aceptación de Servicio Social (Interno TecNM)";
                data["issueDate"] = entity.AcceptanceLetterDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
                data["activitiesOutline"] = entity.Application?.ActivitiesOutline ?? "Apoyo técnico y administrativo en el departamento.";
                break;

            case "commitment":
                data["anexo"] = "ANEXO XX — Carta Compromiso de Servicio Social";
                data["studentAddress"] = entity.Commitment?.StudentAddress ?? entity.Application?.StudentAddress ?? string.Empty;
                data["studentPhone"] = entity.Commitment?.StudentPhone ?? entity.Application?.StudentPhone ?? student.PhoneNumber;
                data["dependencyAddress"] = entity.Commitment?.DependencyAddress ?? entity.Company?.Address;
                data["commitmentDate"] = entity.Commitment?.CommitmentDate ?? entity.StartDate;
                data["city"] = entity.Commitment?.City ?? "Monclova";
                break;

            case "report_1" or "report_2" or "report_3":
                var repNum = int.Parse(normDoc.Split('_')[1]);
                var rep = entity.Reports.SingleOrDefault(r => r.ReportNumber == repNum);
                data["anexo"] = $"ANEXO XXII — Reporte Bimestral #{repNum} de Servicio Social";
                data["reportNumber"] = repNum;
                data["periodStartDate"] = rep?.PeriodStartDate ?? entity.StartDate;
                data["periodEndDate"] = rep?.PeriodEndDate ?? entity.EndDate;
                data["activitiesSummary"] = rep?.ActivitiesSummary ?? entity.Application?.ActivitiesOutline ?? string.Empty;
                data["reportedHours"] = rep?.ReportedHours ?? 160;
                data["reportAccumulatedHours"] = rep?.AccumulatedHours ?? (repNum * 160);
                break;

            case "evaluation_1" or "evaluation_2" or "evaluation_3":
                var evalPeriod = normDoc.Split('_')[1];
                var supEval = entity.Evaluations.SingleOrDefault(
                    e => e.EvaluatorType == "external_supervisor" && e.BimonthlyPeriod == evalPeriod);
                var supCriteria = supEval is null
                    ? ["excellent", "excellent", "excellent", "excellent", "excellent", "excellent", "excellent"]
                    : new[] { supEval.Criterion1, supEval.Criterion2, supEval.Criterion3, supEval.Criterion4, supEval.Criterion5, supEval.Criterion6, supEval.Criterion7 };

                data["anexo"] = $"ANEXO XXIII — Formato de Evaluación Cualitativa del Prestador (Bimestre {evalPeriod})";
                data["bimonthlyPeriod"] = evalPeriod;
                data["evaluatorType"] = "external_supervisor";
                data["criteria"] = AnexoXXIIICriteriaLabels
                    .Select((label, idx) => new { index = idx + 1, label, grade = supCriteria[idx] })
                    .ToList();
                data["observations"] = supEval?.Observations;
                break;

            case "self_evaluation_1" or "self_evaluation_2" or "self_evaluation_3":
                var selfPeriod = normDoc.Split('_')[2];
                var selfEval = entity.Evaluations.SingleOrDefault(
                    e => e.EvaluatorType == "student_self" && e.BimonthlyPeriod == selfPeriod);
                var selfCriteria = selfEval is null
                    ? ["excellent", "excellent", "excellent", "excellent", "excellent", "excellent", "excellent"]
                    : new[] { selfEval.Criterion1, selfEval.Criterion2, selfEval.Criterion3, selfEval.Criterion4, selfEval.Criterion5, selfEval.Criterion6, selfEval.Criterion7 };

                data["anexo"] = $"ANEXO XXIV — Formato de Autoevaluación Cualitativa del Prestador (Bimestre {selfPeriod})";
                data["bimonthlyPeriod"] = selfPeriod;
                data["evaluatorType"] = "student_self";
                data["criteria"] = AnexoXXIVCriteriaLabels
                    .Select((label, idx) => new { index = idx + 1, label, grade = selfCriteria[idx] })
                    .ToList();
                data["observations"] = selfEval?.Observations;
                break;

            case "activity_assessment":
                var act = entity.ActivityAssessment;
                var actAnswers = act is null
                    ? ["excellent", "excellent", "excellent", "excellent", "excellent", "excellent", "excellent", "excellent"]
                    : new[] { act.Question1, act.Question2, act.Question3, act.Question4, act.Question5, act.Question6, act.Question7, act.Question8 };

                data["anexo"] = "ANEXO XXV — Formato de Evaluación de las Actividades por el Prestador de Servicio Social";
                data["questions"] = AnexoXXVQuestionsLabels
                    .Select((label, idx) => new { index = idx + 1, label, grade = actAnswers[idx] })
                    .ToList();
                data["observations"] = act?.Observations;
                break;

            case "release_certificate":
                if (entity.Status != "released")
                {
                    throw new ConflictException(
                        "social_services.not_available",
                        "La Constancia de Terminación solo está disponible cuando el Servicio Social ha sido liberado.");
                }

                data["anexo"] = "Constancia de Terminación de Servicio Social";
                data["folio"] = entity.ReleaseCertificateFolio;
                data["finalGradeLevel"] = entity.FinalGradeLevel ?? "excellent";
                data["finalScore"] = entity.FinalScore;
                break;

            default:
                throw new NotFoundException(
                    "social_services.template_not_found",
                    $"La plantilla '{document}' no es reconocida.");
        }

        return data;
    }

    private IQueryable<Core.Entities.SocialService> BuildBaseQuery() =>
        dbContext.SocialServices
            .Include(s => s.Student)
                .ThenInclude(st => st!.Career)
            .Include(s => s.Period)
            .Include(s => s.Program)
            .Include(s => s.Company)
            .Include(s => s.Application);

    private IQueryable<Core.Entities.SocialService> BuildDetailQuery() =>
        dbContext.SocialServices
            .Include(s => s.Student)
                .ThenInclude(st => st!.Career)
            .Include(s => s.Period)
            .Include(s => s.Program)
            .Include(s => s.Company)
            .Include(s => s.Supervisor)
            .Include(s => s.Agreement)
            .Include(s => s.Application)
                .ThenInclude(a => a!.Reviewer)
            .Include(s => s.Commitment)
            .Include(s => s.Reports)
                .ThenInclude(r => r.Approver)
            .Include(s => s.Evaluations)
            .Include(s => s.ActivityAssessment);

    private async Task<Dictionary<long, List<ExpedientDocument>>> LoadDocumentsByServiceIdsAsync(
        IReadOnlyCollection<long> serviceIds,
        CancellationToken cancellationToken)
    {
        if (serviceIds.Count == 0)
        {
            return new Dictionary<long, List<ExpedientDocument>>();
        }

        var docs = await dbContext.ExpedientDocuments
            .AsNoTracking()
            .Where(d => d.TramiteType == "social_service"
                        && d.TramiteId.HasValue
                        && serviceIds.Contains(d.TramiteId.Value)
                        && d.Status != "superseded")
            .ToListAsync(cancellationToken);

        return docs
            .GroupBy(d => d.TramiteId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    private async Task<bool> HasApprovedDocumentAsync(
        long studentId,
        long tramiteId,
        string documentCode,
        CancellationToken cancellationToken)
    {
        return await dbContext.ExpedientDocuments
            .AnyAsync(d => d.StudentId == studentId
                           && d.TramiteType == "social_service"
                           && d.TramiteId == tramiteId
                           && d.DocumentCode == documentCode
                           && d.Status == "approved",
                cancellationToken);
    }

    private async Task<HashSet<string>> GetApprovedDocumentCodesAsync(
        long studentId,
        long tramiteId,
        CancellationToken cancellationToken)
    {
        var list = await dbContext.ExpedientDocuments
            .AsNoTracking()
            .Where(d => d.StudentId == studentId
                        && d.TramiteType == "social_service"
                        && d.TramiteId == tramiteId
                        && d.Status == "approved")
            .Select(d => d.DocumentCode)
            .ToListAsync(cancellationToken);

        return new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<(AcademicPeriod, SocialServiceProgram, Company, CompanySupervisor, string)> ValidateReferencesAndApplicationAsync(
        long periodId,
        long programId,
        long companyId,
        long supervisorId,
        long? agreementId,
        string? modality,
        DateOnly startDate,
        DateOnly endDate,
        string? targetCommunity,
        SocialServiceApplicationInput? app,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        var period = await dbContext.AcademicPeriods
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == periodId, cancellationToken);
        if (period is null)
        {
            errors["periodId"] = ["El periodo académico especificado no existe."];
        }

        var program = await dbContext.SocialServicePrograms
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == programId && p.IsActive, cancellationToken);
        if (program is null)
        {
            errors["programId"] = ["El programa de Servicio Social no existe o está inactivo."];
        }

        var company = await dbContext.Companies
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == companyId && c.IsActive, cancellationToken);
        if (company is null)
        {
            errors["companyId"] = ["La dependencia receptora no existe o está inactiva."];
        }

        var supervisor = await dbContext.CompanySupervisors
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == supervisorId && s.IsActive, cancellationToken);
        if (supervisor is null)
        {
            errors["supervisorId"] = ["El supervisor seleccionado no existe o está inactivo."];
        }

        var normModality = modality?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normModality is not ("internal" or "external"))
        {
            errors["modality"] = ["La modalidad debe ser 'internal' o 'external'."];
        }

        if (endDate < startDate)
        {
            errors["endDate"] = ["La fecha de término debe ser mayor o igual a la fecha de inicio."];
        }

        if (!string.IsNullOrWhiteSpace(targetCommunity) && targetCommunity.Trim().Length > 200)
        {
            errors["targetCommunity"] = ["La comunidad objetivo no puede exceder 200 caracteres."];
        }

        if (app is null)
        {
            errors["application"] = ["Los datos de la solicitud (Anexo XVIII) son obligatorios."];
        }
        else
        {
            if (string.IsNullOrWhiteSpace(app.StudentAddress))
            {
                errors["application.studentAddress"] = ["El domicilio del estudiante es obligatorio."];
            }

            if (string.IsNullOrWhiteSpace(app.StudentPhone) || app.StudentPhone.Trim().Length > 20)
            {
                errors["application.studentPhone"] = ["El teléfono del estudiante es obligatorio (máx. 20 caracteres)."];
            }

            var pType = app.PeriodType?.Trim().ToLowerInvariant() ?? string.Empty;
            if (pType is not ("august_january" or "february_july"))
            {
                errors["application.periodType"] = ["El tipo de periodo debe ser 'august_january' o 'february_july'."];
            }

            if (string.IsNullOrWhiteSpace(app.DependencyName) || app.DependencyName.Trim().Length > 200)
            {
                errors["application.dependencyName"] = ["El nombre de la dependencia es obligatorio (máx. 200 caracteres)."];
            }

            if (string.IsNullOrWhiteSpace(app.DependencyHeadName) || app.DependencyHeadName.Trim().Length > 180)
            {
                errors["application.dependencyHeadName"] = ["El nombre del titular de la dependencia es obligatorio (máx. 180 caracteres)."];
            }

            if (string.IsNullOrWhiteSpace(app.DependencyHeadPosition) || app.DependencyHeadPosition.Trim().Length > 150)
            {
                errors["application.dependencyHeadPosition"] = ["El cargo del titular es obligatorio (máx. 150 caracteres)."];
            }

            if (string.IsNullOrWhiteSpace(app.ActivitiesOutline))
            {
                errors["application.activitiesOutline"] = ["El resumen de actividades del programa es obligatorio."];
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        if (supervisor!.CompanyId != company!.Id)
        {
            throw new BusinessRuleException(
                "social_services.supervisor_company_mismatch",
                "El supervisor seleccionado no pertenece a la dependencia receptora indicada.");
        }

        var expectedModality = company.IsTecnmInternal ? "internal" : "external";
        if (normModality != expectedModality)
        {
            throw new BusinessRuleException(
                "social_services.modality_mismatch",
                company.IsTecnmInternal
                    ? "La dependencia seleccionada es interna del TecNM; la modalidad debe ser 'internal'."
                    : "La dependencia seleccionada es externa; la modalidad debe ser 'external'.");
        }

        if (agreementId.HasValue)
        {
            var agreement = await dbContext.Agreements
                .AsNoTracking()
                .SingleOrDefaultAsync(a => a.Id == agreementId.Value, cancellationToken);

            if (agreement is null
                || !agreement.IsActive
                || agreement.CompanyId != company.Id
                || agreement.AgreementType is not ("general_framework" or "social_service" or "collaboration")
                || agreement.ExpirationDate < startDate)
            {
                throw new BusinessRuleException(
                    "social_services.agreement_invalid",
                    "El convenio seleccionado no pertenece a la empresa, no es compatible con Servicio Social o no está vigente en la fecha de inicio.");
            }
        }

        return (period!, program!, company, supervisor, normModality);
    }

    private void AddStatusLog(long tramiteId, string? previousStatus, string newStatus, string? reason)
    {
        if (!currentUser.Id.HasValue)
        {
            return;
        }

        dbContext.TramiteStatusLogs.Add(new TramiteStatusLog
        {
            TramiteType = "social_service",
            TramiteId = tramiteId,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            Reason = reason,
            ChangedBy = currentUser.Id.Value
        });
    }

    private async Task<long> ResolveCurrentStudentIdAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.Id.HasValue)
        {
            throw new NotFoundException("students.profile_not_linked", "Sesión no válida.");
        }

        var studentId = await dbContext.Students
            .AsNoTracking()
            .Where(s => s.UserId == currentUser.Id.Value)
            .Select(s => (long?)s.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (!studentId.HasValue)
        {
            throw new NotFoundException(
                "students.profile_not_linked",
                "La cuenta actual no tiene un perfil de estudiante vinculado.");
        }

        return studentId.Value;
    }

    private bool IsSocialServiceStaff() =>
        currentUser.IsInRole("admin") || currentUser.IsInRole("social_service_officer");

    private void EnsureCanViewTramite(Core.Entities.SocialService entity)
    {
        if (currentUser.IsInRole("admin")
            || currentUser.IsInRole("social_service_officer")
            || currentUser.IsInRole("internship_coordinator")
            || currentUser.IsInRole("department_head"))
        {
            return;
        }

        if (!currentUser.Id.HasValue || entity.Student?.UserId != currentUser.Id.Value)
        {
            throw new NotFoundException("social_services.not_found", $"Trámite de Servicio Social #{entity.Id} no encontrado.");
        }
    }

    private void EnsureCanModifyTramite(Core.Entities.SocialService entity, bool allowStudentOnlyWhenRequested)
    {
        if (IsSocialServiceStaff())
        {
            return;
        }

        if (!currentUser.Id.HasValue || entity.Student?.UserId != currentUser.Id.Value)
        {
            throw new NotFoundException("social_services.not_found", $"Trámite de Servicio Social #{entity.Id} no encontrado.");
        }

        if (allowStudentOnlyWhenRequested && entity.Status != "requested")
        {
            throw new ConflictException(
                "social_services.invalid_transition",
                "El estudiante únicamente puede realizar esta acción mientras el trámite se encuentre en estado 'requested'.");
        }
    }

    private static int CalculateDocumentProgressPercentage(
        Core.Entities.SocialService s,
        IReadOnlyCollection<ExpedientDocument> docs)
    {
        var approved = docs
            .Where(d => d.Status == "approved")
            .Select(d => d.DocumentCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var score = 0;

        // 10 columnas de PRINCIPAL CONSULTA en SEGUIMIENTO VINCULACIÓN1.xlsx (10% cada una)
        if (!string.IsNullOrWhiteSpace(s.PresentationLetterFolio) || s.PresentationLetterDate.HasValue || approved.Contains("CP"))
            score += 10;
        if (s.AcceptanceLetterDate.HasValue || approved.Contains("CA"))
            score += 10;
        if (s.Application?.IsAcceptedByOffice == true || approved.Contains("SOL"))
            score += 10;
        if (approved.Contains("CC"))
            score += 10;
        if (approved.Contains("R1"))
            score += 10;
        if (approved.Contains("R2"))
            score += 10;
        if (approved.Contains("R3"))
            score += 10;
        if (approved.Contains("EV1") || approved.Contains("EV2") || approved.Contains("EV3"))
            score += 10;
        if (s.TerminationLetterDate.HasValue || approved.Contains("CT"))
            score += 10;
        if (s.Status == "released" || !string.IsNullOrWhiteSpace(s.ReleaseCertificateFolio) || approved.Contains("COT"))
            score += 10;

        return score;
    }

    private static SocialServiceListItemResponse ToListItemResponse(
        Core.Entities.SocialService s,
        IReadOnlyCollection<ExpedientDocument> docs)
    {
        var student = s.Student;
        var fullName = student is null
            ? "Sin nombre"
            : string.Join(" ", new[] { student.FirstName, student.FirstSurname, student.SecondSurname }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

        var pendingReviews = docs.Count(d => d.Status == "pending");
        var progress = CalculateDocumentProgressPercentage(s, docs);

        return new SocialServiceListItemResponse(
            s.Id,
            s.StudentId,
            student?.ControlNumber ?? string.Empty,
            fullName,
            student?.Career?.Code ?? string.Empty,
            s.Period?.Code ?? string.Empty,
            s.Program?.Name ?? string.Empty,
            s.Company?.Name ?? string.Empty,
            s.Modality,
            s.Status,
            progress,
            s.AccumulatedHours,
            s.RequiredHours,
            s.StartDate,
            s.EndDate,
            pendingReviews,
            s.UpdatedAt);
    }

    private SocialServiceDetailResponse ToDetailResponse(
        Core.Entities.SocialService s,
        IReadOnlyCollection<ExpedientDocument> docs)
    {
        var student = s.Student!;
        var fullName = string.Join(" ", new[] { student.FirstName, student.FirstSurname, student.SecondSurname }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

        var approvedCodes = docs
            .Where(d => d.Status == "approved")
            .Select(d => d.DocumentCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string? DocStatusDetail(string code)
        {
            var doc = docs.FirstOrDefault(d => string.Equals(d.DocumentCode, code, StringComparison.OrdinalIgnoreCase));
            return doc?.Status switch
            {
                "approved" => "Aprobado (SI)",
                "pending" => "En revisión por Vinculación",
                "rejected" => $"Observado: {doc.RejectionReason}",
                _ => "Pendiente de subir PDF firmado y sellado"
            };
        }

        var checklist = new List<TramiteChecklistItem>
        {
            new("imss_uploaded", "Constancia de Vigencia de Derechos IMSS",
                student.HasValidImss || !string.IsNullOrWhiteSpace(student.ImssValidityFileUrl),
                student.HasValidImss ? "Validada" : (!string.IsNullOrWhiteSpace(student.ImssValidityFileUrl) ? "Subida (en revisión)" : "Pendiente")),
            new("document_SOL", "Anexo XVIII — Solicitud de Servicio Social",
                s.Application?.IsAcceptedByOffice == true || approvedCodes.Contains("SOL"),
                s.Application?.IsAcceptedByOffice == true ? "Aprobada (SI)" : DocStatusDetail("SOL")),
            new("presentation_letter", "Carta de Presentación generada (CP)",
                !string.IsNullOrWhiteSpace(s.PresentationLetterFolio) || s.PresentationLetterDate.HasValue || approvedCodes.Contains("CP"),
                s.PresentationLetterFolio ?? "Se genera al aprobar solicitud"),
            new("document_CA", "Carta de Aceptación firmada y sellada",
                s.AcceptanceLetterDate.HasValue || approvedCodes.Contains("CA"),
                DocStatusDetail("CA")),
            new("document_CC", "Anexo XX — Carta Compromiso firmada",
                approvedCodes.Contains("CC"),
                DocStatusDetail("CC")),
            new("document_R1", "Anexo XXII — Reporte Bimestral 1 firmado y sellado",
                approvedCodes.Contains("R1"),
                DocStatusDetail("R1")),
            new("document_EV1", "Anexos XXIII/XXIV — Evaluación Cualitativa 1 firmada y sellada",
                approvedCodes.Contains("EV1"),
                DocStatusDetail("EV1")),
            new("document_R2", "Anexo XXII — Reporte Bimestral 2 firmado y sellado",
                approvedCodes.Contains("R2"),
                DocStatusDetail("R2")),
            new("document_EV2", "Anexos XXIII/XXIV — Evaluación Cualitativa 2 firmada y sellada",
                approvedCodes.Contains("EV2"),
                DocStatusDetail("EV2")),
            new("document_R3", "Anexo XXII — Reporte Bimestral 3 firmado y sellado",
                approvedCodes.Contains("R3"),
                DocStatusDetail("R3")),
            new("document_EV3", "Anexos XXIII/XXIV/XXV — Evaluación Cualitativa 3 y Actividades firmadas y selladas",
                approvedCodes.Contains("EV3"),
                DocStatusDetail("EV3")),
            new("document_CT", "Anexo XIX — Carta de Terminación firmada y sellada",
                s.TerminationLetterDate.HasValue || approvedCodes.Contains("CT"),
                DocStatusDetail("CT")),
            new("release_certificate", "Constancia de Terminación emitida por Vinculación (SSC...)",
                s.Status == "released" || !string.IsNullOrWhiteSpace(s.ReleaseCertificateFolio),
                s.ReleaseCertificateFolio ?? "Pendiente de liberación"),
            new("school_services_act", "Acta enviada a Servicios Escolares (DGTV...)",
                s.SchoolServicesActSent,
                s.SchoolServicesActFolio ?? "Pendiente")
        };

        var allowedActions = BuildAllowedActions(s);
        var progress = CalculateDocumentProgressPercentage(s, docs);

        SocialServiceAgreementSummary? agreementSummary = null;
        if (s.Agreement is not null)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var validity = !s.Agreement.IsActive
                ? "inactive"
                : today > s.Agreement.ExpirationDate
                    ? "expired"
                    : "valid";
            agreementSummary = new SocialServiceAgreementSummary(
                s.Agreement.Id,
                s.Agreement.AgreementNumber,
                validity);
        }

        var app = s.Application;
        var reviewerName = app?.Reviewer is null
            ? null
            : string.Join(" ", new[] { app.Reviewer.FirstName, app.Reviewer.FirstSurname, app.Reviewer.SecondSurname }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

        var appResponse = new SocialServiceApplicationResponse(
            app?.StudentAddress ?? string.Empty,
            app?.StudentPhone ?? student.PhoneNumber ?? string.Empty,
            app?.PeriodType ?? "february_july",
            app?.CurrentSemester ?? s.SemesterEnrolled,
            app?.DependencyName ?? s.Company?.Name ?? string.Empty,
            app?.DependencyHeadName ?? s.Supervisor?.FullName ?? string.Empty,
            app?.DependencyHeadPosition ?? s.Supervisor?.Position ?? string.Empty,
            app?.ActivitiesOutline ?? string.Empty,
            app?.IsAcceptedByOffice,
            app?.RejectionReason,
            app?.OfficeObservations,
            reviewerName,
            app?.ReviewedAt);

        SocialServiceCommitmentResponse? commitmentResp = null;
        if (s.Commitment is not null)
        {
            commitmentResp = new SocialServiceCommitmentResponse(
                s.Commitment.StudentControlNumber,
                s.Commitment.StudentAddress,
                s.Commitment.StudentPhone,
                s.Commitment.DependencyAddress,
                s.Commitment.CommitmentDate,
                s.Commitment.City,
                s.Commitment.IsSignedByStudent,
                s.Commitment.SignedAt);
        }

        return new SocialServiceDetailResponse(
            s.Id,
            new SocialServiceStudentSummary(
                student.Id,
                student.ControlNumber,
                fullName,
                student.Career?.Name ?? string.Empty,
                student.HasValidImss,
                !string.IsNullOrWhiteSpace(student.ImssValidityFileUrl)),
            new SocialServicePeriodSummary(s.PeriodId, s.Period?.Code ?? string.Empty),
            new SocialServiceProgramSummary(
                s.ProgramId,
                s.Program?.Code ?? string.Empty,
                s.Program?.Name ?? string.Empty,
                s.Program?.Category ?? string.Empty),
            new SocialServiceCompanySummary(
                s.CompanyId,
                s.Company?.Name ?? string.Empty,
                s.Company?.IsTecnmInternal ?? false),
            new SocialServiceSupervisorSummary(
                s.SupervisorId,
                s.Supervisor?.FullName ?? string.Empty,
                s.Supervisor?.Position ?? string.Empty),
            agreementSummary,
            s.SemesterEnrolled,
            s.Modality,
            s.StartDate,
            s.EndDate,
            s.RequiredHours,
            s.AccumulatedHours,
            progress,
            s.Status,
            s.PresentationLetterFolio,
            s.PresentationLetterDate,
            s.AcceptanceLetterDate,
            s.TerminationLetterDate,
            s.OriginalTerminationLetterReceived,
            s.ReleaseCertificateFolio,
            s.ReleasedAt,
            s.FinalGradeLevel,
            s.FinalScore,
            s.SchoolServicesActSent,
            s.SchoolServicesActSentAt,
            s.SchoolServicesActFolio,
            s.BeneficiariesCount,
            s.HasInclusionActivities,
            s.TargetCommunity,
            s.Observations,
            s.CancellationReason,
            appResponse,
            commitmentResp,
            s.Reports.OrderBy(r => r.ReportNumber).Select(ToReportResponse).ToList(),
            s.Evaluations.OrderBy(e => e.EvaluatorType).ThenBy(e => e.BimonthlyPeriod).Select(ToEvaluationResponse).ToList(),
            s.ActivityAssessment is null ? null : ToActivityAssessmentResponse(s.ActivityAssessment),
            allowedActions,
            checklist,
            s.CreatedAt,
            s.UpdatedAt);
    }

    private IReadOnlyList<string> BuildAllowedActions(Core.Entities.SocialService s)
    {
        var actions = new List<string>();
        var isStaff = IsSocialServiceStaff();
        var isOwnerStudent = currentUser.IsInRole("student") && currentUser.Id.HasValue && s.Student?.UserId == currentUser.Id.Value;

        if (!isStaff && !isOwnerStudent)
        {
            return actions;
        }

        var isTerminal = TerminalStatuses.Contains(s.Status);

        if (!isTerminal)
        {
            // Formularios de prellenado habilitados sin fricción para alumno y staff
            actions.Add("register_commitment");
            actions.Add("submit_report");
            actions.Add("register_evaluation");
            actions.Add("register_self_evaluation");
            actions.Add("register_activity_assessment");
        }

        if (isOwnerStudent && s.Status == "requested")
        {
            actions.Add("edit");
            actions.Add("cancel");
        }

        if (isStaff)
        {
            if (!isTerminal)
            {
                actions.Add("edit");
                actions.Add("register_presentation_letter");
                actions.Add("review_report");
                actions.Add("update_impact");
                actions.Add("cancel");
                actions.Add("mark_non_compliant");
            }

            switch (s.Status)
            {
                case "requested":
                    actions.Add("review_application");
                    break;
                case "approved":
                    actions.Add("register_acceptance");
                    actions.Add("activate");
                    break;
                case "accepted":
                    actions.Add("activate");
                    break;
                case "active":
                    actions.Add("complete");
                    actions.Add("release");
                    break;
                case "completed_pending_review":
                    actions.Add("release");
                    break;
                case "released":
                    if (!s.SchoolServicesActSent)
                    {
                        actions.Add("send_school_act");
                    }
                    break;
            }
        }

        return actions.Distinct().ToList();
    }

    private static BiMonthlyReportResponse ToReportResponse(SocialServiceBiMonthlyReport r)
    {
        var approverName = r.Approver is null
            ? null
            : string.Join(" ", new[] { r.Approver.FirstName, r.Approver.FirstSurname, r.Approver.SecondSurname }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

        return new BiMonthlyReportResponse(
            r.Id,
            r.ReportNumber,
            r.PeriodStartDate,
            r.PeriodEndDate,
            r.ActivitiesSummary,
            r.ReportedHours,
            r.AccumulatedHours,
            r.EvaluatorSupervisorId,
            r.Status,
            r.OfficeObservations,
            approverName,
            r.ApprovedAt);
    }

    private static SocialServiceEvaluationResponse ToEvaluationResponse(SocialServiceEvaluation e) =>
        new(
            e.Id,
            e.EvaluatorType,
            e.BimonthlyPeriod,
            [e.Criterion1, e.Criterion2, e.Criterion3, e.Criterion4, e.Criterion5, e.Criterion6, e.Criterion7],
            e.EvaluatorSupervisorId,
            e.Observations);

    private static ActivityAssessmentResponse ToActivityAssessmentResponse(SocialServiceActivityAssessment a) =>
        new(
            a.Id,
            [a.Question1, a.Question2, a.Question3, a.Question4, a.Question5, a.Question6, a.Question7, a.Question8],
            a.Observations,
            a.CreatedAt);
}
