using Microsoft.EntityFrameworkCore;
using TecNM.Api.Core.Entities;
using TecNM.Api.Core.Exceptions;
using TecNM.Api.Core.Http;
using TecNM.Api.Core.Security;
using TecNM.Api.Data;
using TecNM.Api.Modules.Catalogs;
using TecNM.Api.Modules.SocialService;

namespace TecNM.Api.Modules.Internships;

public sealed class InternshipsService(
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

    private static readonly HashSet<string> ValidShifts =
    [
        "morning",
        "afternoon",
        "night"
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

    private static readonly string[] AnexoXVIICriteriaLabels =
    [
        "1. Cumple en tiempo y forma con las actividades encomendadas alcanzando los objetivos.",
        "2. Trabaja en equipo y se adapta a nuevas situaciones.",
        "3. Muestra liderazgo en las actividades encomendadas.",
        "4. Organiza su tiempo y trabaja de manera proactiva.",
        "5. Interpreta la realidad y se sensibiliza aportando soluciones a la problemática con la actividad complementaria.",
        "6. Realiza sugerencias innovadoras para beneficio o mejora del programa en el que participa.",
        "7. Tiene iniciativa para ayudar en las actividades encomendadas y muestra espíritu de servicio."
    ];

    public async Task<PagedResponse<InternshipListItemResponse>> GetPagedAsync(
        int page,
        int pageSize,
        long? periodId,
        string? status,
        long? careerId,
        long? companyId,
        bool? isExpedientComplete,
        string? search,
        string? sort,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedSize = Math.Clamp(pageSize, 1, 100);

        var query = BuildBaseQuery().AsNoTracking();

        if (periodId.HasValue)
        {
            query = query.Where(i => i.PeriodId == periodId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normStatus = status.Trim().ToLowerInvariant();
            query = query.Where(i => i.Status == normStatus);
        }

        if (careerId.HasValue)
        {
            query = query.Where(i => i.Student != null && i.Student.CareerId == careerId.Value);
        }

        if (companyId.HasValue)
        {
            query = query.Where(i => i.CompanyId == companyId.Value);
        }

        if (isExpedientComplete.HasValue)
        {
            query = query.Where(i => i.IsExpedientComplete == isExpedientComplete.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            if (term.Length > 100)
            {
                term = term[..100];
            }

            var pattern = $"%{term}%";
            query = query.Where(i =>
                (i.Student != null && (
                    EF.Functions.ILike(i.Student.ControlNumber, pattern) ||
                    EF.Functions.ILike(i.Student.FirstName, pattern) ||
                    EF.Functions.ILike(i.Student.FirstSurname, pattern) ||
                    (i.Student.SecondSurname != null && EF.Functions.ILike(i.Student.SecondSurname, pattern)))) ||
                (i.Company != null && EF.Functions.ILike(i.Company.Name, pattern)));
        }

        query = (sort?.Trim().ToLowerInvariant()) switch
        {
            "controlnumber" => query.OrderBy(i => i.Student!.ControlNumber).ThenByDescending(i => i.Id),
            "-controlnumber" => query.OrderByDescending(i => i.Student!.ControlNumber).ThenByDescending(i => i.Id),
            "startdate" => query.OrderBy(i => i.StartDate).ThenByDescending(i => i.Id),
            "-startdate" => query.OrderByDescending(i => i.StartDate).ThenByDescending(i => i.Id),
            "status" => query.OrderBy(i => i.Status).ThenByDescending(i => i.Id),
            "-status" => query.OrderByDescending(i => i.Status).ThenByDescending(i => i.Id),
            _ => query.OrderByDescending(i => i.UpdatedAt).ThenByDescending(i => i.Id)
        };

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalItems / (double)normalizedSize);

        var items = await query
            .Skip((normalizedPage - 1) * normalizedSize)
            .Take(normalizedSize)
            .ToListAsync(cancellationToken);

        var ids = items.Select(x => x.Id).ToList();
        var docsByInternship = await LoadDocumentsByInternshipIdsAsync(ids, cancellationToken);

        var responses = items
            .Select(item =>
            {
                docsByInternship.TryGetValue(item.Id, out var docs);
                return ToListItemResponse(item, docs ?? []);
            })
            .ToList();

        return new PagedResponse<InternshipListItemResponse>(
            responses,
            normalizedPage,
            normalizedSize,
            totalItems,
            totalPages);
    }

    public async Task<IReadOnlyList<InternshipListItemResponse>> GetMyListAsync(
        CancellationToken cancellationToken = default)
    {
        var studentId = await ResolveCurrentStudentIdAsync(cancellationToken);

        var items = await BuildBaseQuery()
            .AsNoTracking()
            .Where(i => i.StudentId == studentId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

        var ids = items.Select(x => x.Id).ToList();
        var docsByInternship = await LoadDocumentsByInternshipIdsAsync(ids, cancellationToken);

        return items
            .Select(item =>
            {
                docsByInternship.TryGetValue(item.Id, out var docs);
                return ToListItemResponse(item, docs ?? []);
            })
            .ToList();
    }

    public async Task<InternshipDetailResponse> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await BuildDetailQuery()
            .AsNoTracking()
            .SingleOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException("internships.not_found", $"Trámite de Prácticas Profesionales #{id} no encontrado.");

        EnsureCanViewTramite(entity);

        var docs = await dbContext.ExpedientDocuments
            .AsNoTracking()
            .Where(d => d.StudentId == entity.StudentId
                        && d.TramiteType == "internship"
                        && d.TramiteId == entity.Id
                        && d.Status != "superseded")
            .ToListAsync(cancellationToken);

        return ToDetailResponse(entity, docs);
    }

    public async Task<InternshipDetailResponse> CreateAsync(
        CreateInternshipRequest request,
        CancellationToken cancellationToken = default)
    {
        long targetStudentId;
        if (IsInternshipStaff())
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
                "internships.student_not_eligible",
                "El estudiante no se encuentra inscrito o en condiciones de realizar Prácticas Profesionales.");
        }

        if (!student.HasValidImss && string.IsNullOrWhiteSpace(student.ImssValidityFileUrl))
        {
            var hasImssDoc = await dbContext.ExpedientDocuments
                .AnyAsync(d => d.StudentId == student.Id && d.DocumentCode == "IMSS" && d.Status != "superseded", cancellationToken);

            if (!hasImssDoc && !IsInternshipStaff())
            {
                throw new BusinessRuleException(
                    "internships.student_not_eligible",
                    "Debe adjuntar primero su Constancia de Vigencia de Derechos del IMSS en PDF para solicitar la Carta de Presentación.");
            }
        }

        var hasActiveOrReleased = await dbContext.Internships
            .AnyAsync(i => i.StudentId == targetStudentId && i.Status != "cancelled" && i.Status != "non_compliant", cancellationToken);

        if (hasActiveOrReleased)
        {
            throw new BusinessRuleException(
                "internships.student_not_eligible",
                "El estudiante ya cuenta con un trámite de Prácticas Profesionales activo o liberado.");
        }

        var normalizedShift = await ValidateCreateOrUpdateRequestAsync(request, cancellationToken);
        var requiredHours = request.RequiredHours ?? 200;

        // Actualizar datos confirmados por el alumno en el formulario de apertura (Forms)
        student.CurrentSemester = request.CurrentSemester;
        student.Shift = normalizedShift;
        student.PhoneNumber = request.PhoneNumber.Trim();
        student.InstitutionalEmail = request.InstitutionalEmail.Trim();

        var entity = new Internship
        {
            StudentId = student.Id,
            PeriodId = request.PeriodId,
            CompanyId = request.CompanyId,
            SupervisorId = request.SupervisorId,
            AgreementId = request.AgreementId,
            SemesterEnrolled = request.CurrentSemester,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            ScheduleDescription = string.IsNullOrWhiteSpace(request.ScheduleDescription) ? null : request.ScheduleDescription.Trim(),
            RequiredHours = requiredHours,
            CompletedHours = 0,
            Status = "requested",
            CreditsAwarded = 2
        };

        dbContext.Internships.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        AddStatusLog(entity.Id, null, "requested", "Solicitud de Carta de Presentación de Prácticas Profesionales");
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<InternshipDetailResponse> UpdateAsync(
        long id,
        CreateInternshipRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Internships
            .Include(i => i.Student)
            .SingleOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException("internships.not_found", $"Trámite de Prácticas Profesionales #{id} no encontrado.");

        EnsureCanModifyTramite(entity, allowStudentOnlyWhenRequested: true);

        if (TerminalStatuses.Contains(entity.Status))
        {
            throw new ConflictException(
                "internships.invalid_transition",
                $"No se puede editar un trámite en estado terminal ('{entity.Status}').");
        }

        var normalizedShift = await ValidateCreateOrUpdateRequestAsync(request, cancellationToken);

        entity.PeriodId = request.PeriodId;
        entity.SemesterEnrolled = request.CurrentSemester;
        entity.CompanyId = request.CompanyId;
        entity.SupervisorId = request.SupervisorId;
        entity.AgreementId = request.AgreementId;
        entity.StartDate = request.StartDate;
        entity.EndDate = request.EndDate;
        entity.ScheduleDescription = string.IsNullOrWhiteSpace(request.ScheduleDescription) ? null : request.ScheduleDescription.Trim();
        entity.RequiredHours = request.RequiredHours ?? entity.RequiredHours;

        if (entity.Student is not null)
        {
            entity.Student.CurrentSemester = request.CurrentSemester;
            entity.Student.Shift = normalizedShift;
            entity.Student.PhoneNumber = request.PhoneNumber.Trim();
            entity.Student.InstitutionalEmail = request.InstitutionalEmail.Trim();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<InternshipDetailResponse> ApproveAsync(
        long id,
        ApproveInternshipRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Internships
            .SingleOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException("internships.not_found", $"Trámite de Prácticas Profesionales #{id} no encontrado.");

        if (entity.Status != "requested")
        {
            throw new ConflictException(
                "internships.invalid_transition",
                $"La solicitud solo puede aprobarse desde el estado 'requested' (actual: '{entity.Status}').");
        }

        var previous = entity.Status;
        entity.Status = "approved";
        entity.PresentationLetterDate ??= DateOnly.FromDateTime(DateTime.UtcNow);
        if (!string.IsNullOrWhiteSpace(request.Observations))
        {
            entity.Observations = request.Observations.Trim();
        }

        if (string.IsNullOrWhiteSpace(entity.PresentationLetterFolio))
        {
            entity.PresentationLetterFolio = await documentSeriesService.GenerateNextFolioAsync(
                "internship_presentation_letter",
                cancellationToken);
        }

        AddStatusLog(entity.Id, previous, "approved", $"Solicitud aprobada y Carta de Presentación emitida ({entity.PresentationLetterFolio})");
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<InternshipDetailResponse> RegisterPresentationLetterAsync(
        long id,
        InternshipPresentationLetterRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Internships
            .SingleOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException("internships.not_found", $"Trámite de Prácticas Profesionales #{id} no encontrado.");

        if (TerminalStatuses.Contains(entity.Status))
        {
            throw new ConflictException("internships.invalid_transition", "El trámite se encuentra en un estado terminal.");
        }

        entity.PresentationLetterDate = request.PresentationLetterDate;
        if (string.IsNullOrWhiteSpace(entity.PresentationLetterFolio))
        {
            entity.PresentationLetterFolio = await documentSeriesService.GenerateNextFolioAsync(
                "internship_presentation_letter",
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<InternshipDetailResponse> RegisterAcceptanceAsync(
        long id,
        RegisterInternshipAcceptanceRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Internships
            .Include(i => i.Student)
            .SingleOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException("internships.not_found", $"Trámite de Prácticas Profesionales #{id} no encontrado.");

        EnsureCanModifyTramite(entity, allowStudentOnlyWhenRequested: false);

        if (TerminalStatuses.Contains(entity.Status))
        {
            throw new ConflictException("internships.invalid_transition", "El trámite se encuentra en un estado terminal.");
        }

        await ValidateCompanySupervisorAndAgreementAsync(
            request.CompanyId,
            request.SupervisorId,
            request.AgreementId,
            request.StartDate,
            request.EndDate,
            request.RequiredHours ?? 200,
            cancellationToken);

        entity.CompanyId = request.CompanyId;
        entity.SupervisorId = request.SupervisorId;
        entity.AgreementId = request.AgreementId;
        entity.StartDate = request.StartDate;
        entity.EndDate = request.EndDate;
        entity.ScheduleDescription = string.IsNullOrWhiteSpace(request.ScheduleDescription) ? null : request.ScheduleDescription.Trim();
        entity.RequiredHours = request.RequiredHours ?? entity.RequiredHours;
        entity.AcceptanceLetterDate = request.AcceptanceLetterDate;

        var hasApprovedCa = await HasApprovedDocumentAsync(entity.StudentId, entity.Id, "CA", cancellationToken);
        if (IsInternshipStaff() || hasApprovedCa)
        {
            if (entity.Status is "requested" or "approved" or "accepted")
            {
                var previous = entity.Status;
                entity.Status = "active";
                AddStatusLog(entity.Id, previous, "active", "Oficio de Aceptación registrado e inicio de Prácticas Profesionales");
            }
        }
        else if (entity.Status == "approved")
        {
            var previous = entity.Status;
            entity.Status = "accepted";
            AddStatusLog(entity.Id, previous, "accepted", "Datos de Oficio de Aceptación registrados por el estudiante");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<InternshipDetailResponse> ActivateAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Internships
            .SingleOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException("internships.not_found", $"Trámite de Prácticas Profesionales #{id} no encontrado.");

        if (entity.Status is not ("approved" or "accepted"))
        {
            throw new ConflictException(
                "internships.invalid_transition",
                $"Se requiere estado 'approved' o 'accepted' para activar las Prácticas Profesionales (actual: '{entity.Status}').");
        }

        if (!entity.CompanyId.HasValue || !entity.SupervisorId.HasValue)
        {
            throw new BusinessRuleException(
                "internships.company_required",
                "Debe registrarse la empresa y el supervisor del Oficio de Aceptación antes de activar las Prácticas Profesionales.");
        }

        var hasApprovedCa = await HasApprovedDocumentAsync(entity.StudentId, entity.Id, "CA", cancellationToken);
        if (!hasApprovedCa)
        {
            throw new BusinessRuleException(
                "internships.documents_missing",
                "Se requiere que el PDF del Oficio de Aceptación ('CA') firmado y sellado esté aprobado.");
        }

        var previous = entity.Status;
        entity.Status = "active";
        entity.AcceptanceLetterDate ??= DateOnly.FromDateTime(DateTime.UtcNow);
        AddStatusLog(entity.Id, previous, "active", "Inicio formal de Prácticas Profesionales (Oficio de Aceptación validado)");

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<InternshipEvaluationResponse> UpsertEvaluationAsync(
        long id,
        InternshipEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.EvaluationEndDate < request.EvaluationStartDate)
        {
            errors["evaluationEndDate"] = ["La fecha final de evaluación debe ser mayor o igual a la fecha inicial."];
        }

        var criteria = request.Criteria is { Count: 7 }
            ? request.Criteria.ToArray()
            : [4, 4, 4, 4, 4, 4, 4];

        if (criteria.Any(c => c is < 0 or > 4))
        {
            errors["criteria"] = ["Los 7 criterios de evaluación deben ser números enteros entre 0 y 4."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        var entity = await dbContext.Internships
            .Include(i => i.Student)
            .Include(i => i.Evaluation)
            .SingleOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException("internships.not_found", $"Trámite de Prácticas Profesionales #{id} no encontrado.");

        EnsureCanModifyTramite(entity, allowStudentOnlyWhenRequested: false);

        var avg = Math.Round((decimal)criteria.Average(), 2);
        var level = avg switch
        {
            >= 3.50m => "excellent",
            >= 2.50m => "notable",
            >= 1.50m => "good",
            >= 1.00m => "sufficient",
            _ => "insufficient"
        };
        var achieved = avg >= 1.00m;

        if (entity.Evaluation is null)
        {
            entity.Evaluation = new InternshipEvaluation
            {
                InternshipId = entity.Id,
                ActivityName = "PRODUCTIVIDAD LABORAL",
                EvaluationStartDate = request.EvaluationStartDate,
                EvaluationEndDate = request.EvaluationEndDate,
                Criterion1 = criteria[0],
                Criterion2 = criteria[1],
                Criterion3 = criteria[2],
                Criterion4 = criteria[3],
                Criterion5 = criteria[4],
                Criterion6 = criteria[5],
                Criterion7 = criteria[6],
                NumericalAverage = avg,
                PerformanceLevel = level,
                IsCompetencyAchieved = achieved,
                EvaluatorSupervisorId = request.EvaluatorSupervisorId ?? entity.SupervisorId,
                EvaluatorSignatureDate = request.EvaluatorSignatureDate,
                Observations = string.IsNullOrWhiteSpace(request.Observations) ? null : request.Observations.Trim()
            };
            dbContext.InternshipEvaluations.Add(entity.Evaluation);
        }
        else
        {
            entity.Evaluation.EvaluationStartDate = request.EvaluationStartDate;
            entity.Evaluation.EvaluationEndDate = request.EvaluationEndDate;
            entity.Evaluation.Criterion1 = criteria[0];
            entity.Evaluation.Criterion2 = criteria[1];
            entity.Evaluation.Criterion3 = criteria[2];
            entity.Evaluation.Criterion4 = criteria[3];
            entity.Evaluation.Criterion5 = criteria[4];
            entity.Evaluation.Criterion6 = criteria[5];
            entity.Evaluation.Criterion7 = criteria[6];
            entity.Evaluation.NumericalAverage = avg;
            entity.Evaluation.PerformanceLevel = level;
            entity.Evaluation.IsCompetencyAchieved = achieved;
            entity.Evaluation.EvaluatorSupervisorId = request.EvaluatorSupervisorId ?? entity.SupervisorId;
            entity.Evaluation.EvaluatorSignatureDate = request.EvaluatorSignatureDate;
            entity.Evaluation.Observations = string.IsNullOrWhiteSpace(request.Observations) ? null : request.Observations.Trim();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToEvaluationResponse(entity.Evaluation);
    }

    public async Task<InternshipDetailResponse> UpdateExpedientAsync(
        long id,
        UpdateInternshipExpedientRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Internships
            .SingleOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException("internships.not_found", $"Trámite de Prácticas Profesionales #{id} no encontrado.");

        entity.IsExpedientComplete = request.IsExpedientComplete;
        entity.OriginalTerminationLetterReceived = request.OriginalTerminationLetterReceived;
        entity.Observations = string.IsNullOrWhiteSpace(request.Observations) ? null : request.Observations.Trim();

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<InternshipDetailResponse> CompleteAsync(
        long id,
        CompleteInternshipRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Internships
            .SingleOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException("internships.not_found", $"Trámite de Prácticas Profesionales #{id} no encontrado.");

        if (entity.Status is not ("active" or "accepted" or "approved"))
        {
            throw new ConflictException(
                "internships.invalid_transition",
                $"No se puede marcar como concluido desde el estado '{entity.Status}'.");
        }

        var approvedCodes = await GetApprovedDocumentCodesAsync(entity.StudentId, entity.Id, cancellationToken);
        var requiredDocs = new[] { "CA", "EV", "CT" };
        var missing = requiredDocs.Where(code => !approvedCodes.Contains(code)).ToList();

        if (missing.Count > 0)
        {
            throw new BusinessRuleException(
                "internships.documents_missing",
                $"Faltan documentos firmados y sellados por aprobar en el expediente: {string.Join(", ", missing)}.");
        }

        var previous = entity.Status;
        entity.TerminationLetterDate = request.TerminationLetterDate;
        entity.Status = "completed_pending_review";
        AddStatusLog(entity.Id, previous, "completed_pending_review", "Expediente documental de Prácticas Profesionales concluido");

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<InternshipDetailResponse> ReleaseAsync(
        long id,
        ReleaseInternshipRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Internships
            .SingleOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException("internships.not_found", $"Trámite de Prácticas Profesionales #{id} no encontrado.");

        if (entity.Status is not ("completed_pending_review" or "active"))
        {
            throw new ConflictException(
                "internships.invalid_transition",
                $"El trámite debe estar en 'completed_pending_review' o 'active' para liberar (actual: '{entity.Status}').");
        }

        var approvedCodes = await GetApprovedDocumentCodesAsync(entity.StudentId, entity.Id, cancellationToken);
        var requiredDocs = new[] { "CA", "EV", "CT" };
        var missing = requiredDocs.Where(code => !approvedCodes.Contains(code)).ToList();

        if (missing.Count > 0)
        {
            throw new BusinessRuleException(
                "internships.documents_missing",
                $"No se puede liberar porque faltan documentos aprobados: {string.Join(", ", missing)}.");
        }

        var folio = await documentSeriesService.GenerateNextFolioAsync(
            "internship_release_certificate",
            cancellationToken);

        var previous = entity.Status;
        entity.Status = "released";
        entity.IsExpedientComplete = true;
        entity.OriginalTerminationLetterReceived = true;
        entity.ReleaseCertificateFolio = folio;
        entity.ReleaseDate = request.ReleaseDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        entity.ReleasedAt = DateTimeOffset.UtcNow;
        entity.TerminationLetterDate ??= entity.ReleaseDate;

        AddStatusLog(entity.Id, previous, "released", $"Constancia de Acreditación de Prácticas emitida con folio {folio}");

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<InternshipDetailResponse> SendSchoolServicesActAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Internships
            .SingleOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException("internships.not_found", $"Trámite de Prácticas Profesionales #{id} no encontrado.");

        if (entity.Status != "released")
        {
            throw new ConflictException(
                "internships.invalid_transition",
                "Únicamente los trámites liberados ('released') pueden enviarse en acta a Servicios Escolares.");
        }

        if (entity.SchoolServicesActSent)
        {
            throw new ConflictException(
                "internships.act_already_sent",
                $"El acta a Servicios Escolares ya fue enviada previamente con el folio '{entity.SchoolServicesActFolio}'.");
        }

        var folio = await documentSeriesService.GenerateNextFolioAsync("school_services_act", cancellationToken);
        entity.SchoolServicesActSent = true;
        entity.SchoolServicesActSentAt = DateTimeOffset.UtcNow;
        entity.SchoolServicesActFolio = folio;

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<InternshipDetailResponse> CancelAsync(
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

        var entity = await dbContext.Internships
            .Include(i => i.Student)
            .SingleOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException("internships.not_found", $"Trámite de Prácticas Profesionales #{id} no encontrado.");

        EnsureCanModifyTramite(entity, allowStudentOnlyWhenRequested: true);

        if (TerminalStatuses.Contains(entity.Status))
        {
            throw new ConflictException(
                "internships.invalid_transition",
                $"No se puede cancelar un trámite en estado terminal ('{entity.Status}').");
        }

        var previous = entity.Status;
        entity.Status = "cancelled";
        entity.CancellationReason = request.Reason.Trim();
        AddStatusLog(entity.Id, previous, "cancelled", entity.CancellationReason);

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<InternshipDetailResponse> MarkNonComplianceAsync(
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
            errors["description"] = ["La descripción de Producto No Conforme es obligatoria."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        var entity = await dbContext.Internships
            .SingleOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException("internships.not_found", $"Trámite de Prácticas Profesionales #{id} no encontrado.");

        if (TerminalStatuses.Contains(entity.Status))
        {
            throw new ConflictException(
                "internships.invalid_transition",
                $"No se puede marcar como No Conforme un trámite en estado terminal ('{entity.Status}').");
        }

        var previous = entity.Status;
        entity.Status = "non_compliant";
        entity.CancellationReason = request.Description.Trim();

        var incidentCode = $"PNC-PP-{DateTime.UtcNow.Year}-{entity.Id:D4}-{Random.Shared.Next(10, 99)}";
        var pnc = new NonConformance
        {
            TramiteType = "internship",
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
        var entity = await dbContext.Internships
            .Include(i => i.Student)
            .AsNoTracking()
            .SingleOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException("internships.not_found", $"Trámite de Prácticas Profesionales #{id} no encontrado.");

        EnsureCanViewTramite(entity);

        var logs = await dbContext.TramiteStatusLogs
            .Include(l => l.ChangedByUser)
            .AsNoTracking()
            .Where(l => l.TramiteType == "internship" && l.TramiteId == id)
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
            .SingleOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException("internships.not_found", $"Trámite de Prácticas Profesionales #{id} no encontrado.");

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
            ["studentGender"] = student.Gender,
            ["studentShift"] = student.Shift,
            ["studentSemester"] = entity.SemesterEnrolled,
            ["studentPhone"] = student.PhoneNumber,
            ["studentInstitutionalEmail"] = student.InstitutionalEmail,
            ["careerCode"] = student.Career?.Code,
            ["careerName"] = student.Career?.Name,
            ["periodCode"] = entity.Period?.Code,
            ["periodName"] = entity.Period?.Name,
            ["companyName"] = entity.Company?.Name ?? "A QUIEN CORRESPONDA",
            ["companySector"] = entity.Company?.Sector,
            ["supervisorFullName"] = entity.Supervisor?.FullName ?? "A QUIEN CORRESPONDA",
            ["supervisorPosition"] = entity.Supervisor?.Position ?? "PRESENTE",
            ["startDate"] = entity.StartDate,
            ["endDate"] = entity.EndDate,
            ["scheduleDescription"] = entity.ScheduleDescription,
            ["requiredHours"] = entity.RequiredHours,
            ["creditsAwarded"] = entity.CreditsAwarded,
            ["acceptanceEmailNotice"] = "arturo.martinez@monclova.tecnm.mx",
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
            case "presentation_letter":
                data["anexo"] = "Carta de Presentación de Prácticas Profesionales";
                data["folio"] = entity.PresentationLetterFolio ?? "PENDIENTE";
                data["issueDate"] = entity.PresentationLetterDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
                break;

            case "evaluation":
                var eval = entity.Evaluation;
                var evalScores = eval is null
                    ? [4, 4, 4, 4, 4, 4, 4]
                    : new[] { eval.Criterion1, eval.Criterion2, eval.Criterion3, eval.Criterion4, eval.Criterion5, eval.Criterion6, eval.Criterion7 };

                data["anexo"] = "ANEXO XVII — Formato de Evaluación al Desempeño de la Actividad Complementaria";
                data["activityName"] = eval?.ActivityName ?? "PRODUCTIVIDAD LABORAL";
                data["evaluationStartDate"] = eval?.EvaluationStartDate ?? entity.StartDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
                data["evaluationEndDate"] = eval?.EvaluationEndDate ?? entity.EndDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
                data["criteria"] = AnexoXVIICriteriaLabels
                    .Select((label, idx) => new { index = idx + 1, label, score = evalScores[idx] })
                    .ToList();
                data["numericalAverage"] = eval?.NumericalAverage ?? 4.00m;
                data["performanceLevel"] = eval?.PerformanceLevel ?? "excellent";
                data["isCompetencyAchieved"] = eval?.IsCompetencyAchieved ?? true;
                data["evaluatorSignatureDate"] = eval?.EvaluatorSignatureDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
                data["observations"] = eval?.Observations;
                break;

            case "release_certificate":
                if (entity.Status != "released")
                {
                    throw new ConflictException(
                        "internships.not_available",
                        "La Constancia de Acreditación de Prácticas Profesionales solo está disponible cuando el trámite ha sido liberado.");
                }

                data["anexo"] = "Constancia de Cumplimiento de Actividad Complementaria (Prácticas Profesionales)";
                data["folio"] = entity.ReleaseCertificateFolio;
                data["releaseDate"] = entity.ReleaseDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
                data["numericalAverage"] = entity.Evaluation?.NumericalAverage ?? 4.00m;
                data["performanceLevel"] = entity.Evaluation?.PerformanceLevel ?? "excellent";
                break;

            default:
                throw new NotFoundException(
                    "internships.template_not_found",
                    $"La plantilla '{document}' no es reconocida.");
        }

        return data;
    }

    private IQueryable<Internship> BuildBaseQuery() =>
        dbContext.Internships
            .Include(i => i.Student)
                .ThenInclude(s => s!.Career)
            .Include(i => i.Period)
            .Include(i => i.Company);

    private IQueryable<Internship> BuildDetailQuery() =>
        dbContext.Internships
            .Include(i => i.Student)
                .ThenInclude(s => s!.Career)
            .Include(i => i.Period)
            .Include(i => i.Company)
            .Include(i => i.Supervisor)
            .Include(i => i.Agreement)
            .Include(i => i.Evaluation);

    private async Task<Dictionary<long, List<ExpedientDocument>>> LoadDocumentsByInternshipIdsAsync(
        IReadOnlyCollection<long> internshipIds,
        CancellationToken cancellationToken)
    {
        if (internshipIds.Count == 0)
        {
            return new Dictionary<long, List<ExpedientDocument>>();
        }

        var docs = await dbContext.ExpedientDocuments
            .AsNoTracking()
            .Where(d => d.TramiteType == "internship"
                        && d.TramiteId.HasValue
                        && internshipIds.Contains(d.TramiteId.Value)
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
                           && d.TramiteType == "internship"
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
                        && d.TramiteType == "internship"
                        && d.TramiteId == tramiteId
                        && d.Status == "approved")
            .Select(d => d.DocumentCode)
            .ToListAsync(cancellationToken);

        return new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<string> ValidateCreateOrUpdateRequestAsync(
        CreateInternshipRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        var periodExists = await dbContext.AcademicPeriods
            .AnyAsync(p => p.Id == request.PeriodId, cancellationToken);
        if (!periodExists)
        {
            errors["periodId"] = ["El periodo académico especificado no existe."];
        }

        if (request.CurrentSemester is < 1 or > 15)
        {
            errors["currentSemester"] = ["El semestre debe estar entre 1 y 15."];
        }

        var normShift = request.Shift?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!ValidShifts.Contains(normShift))
        {
            errors["shift"] = ["El turno debe ser 'morning', 'afternoon' o 'night'."];
        }

        if (string.IsNullOrWhiteSpace(request.PhoneNumber) || request.PhoneNumber.Trim().Length > 20)
        {
            errors["phoneNumber"] = ["El teléfono de contacto es obligatorio (máx. 20 caracteres)."];
        }

        if (string.IsNullOrWhiteSpace(request.InstitutionalEmail)
            || request.InstitutionalEmail.Trim().Length > 255
            || !request.InstitutionalEmail.Contains('@'))
        {
            errors["institutionalEmail"] = ["El correo electrónico institucional es obligatorio y debe ser válido."];
        }

        var reqHours = request.RequiredHours ?? 200;
        if (reqHours is < 200 or > 480)
        {
            errors["requiredHours"] = ["Las horas requeridas deben estar entre 200 y 480."];
        }

        if (request.StartDate.HasValue && request.EndDate.HasValue && request.EndDate.Value < request.StartDate.Value)
        {
            errors["endDate"] = ["La fecha de término debe ser mayor o igual a la fecha de inicio."];
        }

        if (!string.IsNullOrWhiteSpace(request.ScheduleDescription) && request.ScheduleDescription.Trim().Length > 150)
        {
            errors["scheduleDescription"] = ["El horario no puede exceder 150 caracteres."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        if (request.CompanyId.HasValue || request.SupervisorId.HasValue)
        {
            if (!request.CompanyId.HasValue || !request.SupervisorId.HasValue)
            {
                throw new ValidationException(new Dictionary<string, string[]>
                {
                    ["companyId"] = ["Si especifica empresa o supervisor, ambos campos son obligatorios."]
                });
            }

            await ValidateCompanySupervisorAndAgreementAsync(
                request.CompanyId.Value,
                request.SupervisorId.Value,
                request.AgreementId,
                request.StartDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                request.EndDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                reqHours,
                cancellationToken);
        }

        return normShift;
    }

    private async Task ValidateCompanySupervisorAndAgreementAsync(
        long companyId,
        long supervisorId,
        long? agreementId,
        DateOnly startDate,
        DateOnly endDate,
        int requiredHours,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (endDate < startDate)
        {
            errors["endDate"] = ["La fecha de término debe ser mayor o igual a la fecha de inicio."];
        }

        if (requiredHours is < 200 or > 480)
        {
            errors["requiredHours"] = ["Las horas requeridas deben estar entre 200 y 480."];
        }

        var company = await dbContext.Companies
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == companyId && c.IsActive, cancellationToken);
        if (company is null)
        {
            errors["companyId"] = ["La empresa seleccionada no existe o está inactiva."];
        }

        var supervisor = await dbContext.CompanySupervisors
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == supervisorId && s.IsActive, cancellationToken);
        if (supervisor is null)
        {
            errors["supervisorId"] = ["El supervisor seleccionado no existe o está inactivo."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        if (supervisor!.CompanyId != company!.Id)
        {
            throw new BusinessRuleException(
                "internships.supervisor_company_mismatch",
                "El supervisor seleccionado no pertenece a la empresa receptora indicada.");
        }

        if (agreementId.HasValue)
        {
            var agreement = await dbContext.Agreements
                .AsNoTracking()
                .SingleOrDefaultAsync(a => a.Id == agreementId.Value, cancellationToken);

            if (agreement is null
                || !agreement.IsActive
                || agreement.CompanyId != company.Id
                || agreement.AgreementType is not ("general_framework" or "internship" or "dual_education" or "collaboration")
                || agreement.ExpirationDate < startDate)
            {
                throw new BusinessRuleException(
                    "internships.agreement_invalid",
                    "El convenio seleccionado no pertenece a la empresa, no es compatible con Prácticas Profesionales o no está vigente.");
            }
        }
    }

    private void AddStatusLog(long tramiteId, string? previousStatus, string newStatus, string? reason)
    {
        if (!currentUser.Id.HasValue)
        {
            return;
        }

        dbContext.TramiteStatusLogs.Add(new TramiteStatusLog
        {
            TramiteType = "internship",
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

    private bool IsInternshipStaff() =>
        currentUser.IsInRole("admin") || currentUser.IsInRole("internship_coordinator");

    private void EnsureCanViewTramite(Internship entity)
    {
        if (currentUser.IsInRole("admin")
            || currentUser.IsInRole("internship_coordinator")
            || currentUser.IsInRole("social_service_officer")
            || currentUser.IsInRole("department_head"))
        {
            return;
        }

        if (!currentUser.Id.HasValue || entity.Student?.UserId != currentUser.Id.Value)
        {
            throw new NotFoundException("internships.not_found", $"Trámite de Prácticas Profesionales #{entity.Id} no encontrado.");
        }
    }

    private void EnsureCanModifyTramite(Internship entity, bool allowStudentOnlyWhenRequested)
    {
        if (IsInternshipStaff())
        {
            return;
        }

        if (!currentUser.Id.HasValue || entity.Student?.UserId != currentUser.Id.Value)
        {
            throw new NotFoundException("internships.not_found", $"Trámite de Prácticas Profesionales #{entity.Id} no encontrado.");
        }

        if (allowStudentOnlyWhenRequested && entity.Status != "requested")
        {
            throw new ConflictException(
                "internships.invalid_transition",
                "El estudiante únicamente puede realizar esta acción mientras el trámite se encuentre en estado 'requested'.");
        }
    }

    private static int CalculateDocumentProgressPercentage(
        Internship i,
        IReadOnlyCollection<ExpedientDocument> docs)
    {
        var approved = docs
            .Where(d => d.Status == "approved")
            .Select(d => d.DocumentCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var score = 0;

        // 5 hitos en SEGUIMIENTO VINCULACIÓN1.xlsx (20% cada uno): CP, CA, EV, CT, CC
        if (!string.IsNullOrWhiteSpace(i.PresentationLetterFolio) || i.PresentationLetterDate.HasValue || approved.Contains("CP"))
            score += 20;
        if (i.AcceptanceLetterDate.HasValue || approved.Contains("CA"))
            score += 20;
        if (approved.Contains("EV"))
            score += 20;
        if (i.TerminationLetterDate.HasValue || approved.Contains("CT"))
            score += 20;
        if (i.Status == "released" || i.IsExpedientComplete || !string.IsNullOrWhiteSpace(i.ReleaseCertificateFolio) || approved.Contains("CC"))
            score += 20;

        return score;
    }

    private static InternshipListItemResponse ToListItemResponse(
        Internship i,
        IReadOnlyCollection<ExpedientDocument> docs)
    {
        var student = i.Student;
        var fullName = student is null
            ? "Sin nombre"
            : string.Join(" ", new[] { student.FirstName, student.FirstSurname, student.SecondSurname }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

        var pendingReviews = docs.Count(d => d.Status == "pending");
        var progress = CalculateDocumentProgressPercentage(i, docs);

        return new InternshipListItemResponse(
            i.Id,
            i.StudentId,
            student?.ControlNumber ?? string.Empty,
            fullName,
            student?.Career?.Code ?? string.Empty,
            i.Period?.Code ?? string.Empty,
            i.Company?.Name,
            i.Status,
            progress,
            i.IsExpedientComplete,
            i.PresentationLetterFolio,
            i.StartDate,
            i.EndDate,
            pendingReviews,
            i.UpdatedAt);
    }

    private InternshipDetailResponse ToDetailResponse(
        Internship i,
        IReadOnlyCollection<ExpedientDocument> docs)
    {
        var student = i.Student!;
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
            new("presentation_letter", "Carta de Presentación emitida (Oficio No. EJ26/PR...)",
                !string.IsNullOrWhiteSpace(i.PresentationLetterFolio) || i.PresentationLetterDate.HasValue || approvedCodes.Contains("CP"),
                i.PresentationLetterFolio ?? "Se genera al aprobar solicitud"),
            new("document_CA", "Oficio de Aceptación (hoja membretada, firma y sello -> arturo.martinez@monclova.tecnm.mx)",
                i.AcceptanceLetterDate.HasValue || approvedCodes.Contains("CA"),
                DocStatusDetail("CA")),
            new("document_EV", "Anexo XVII — Evaluación de la empresa firmada y sellada",
                approvedCodes.Contains("EV"),
                DocStatusDetail("EV")),
            new("document_CT", "Oficio de Terminación (hoja membretada, firma y sello)",
                i.TerminationLetterDate.HasValue || approvedCodes.Contains("CT"),
                DocStatusDetail("CT")),
            new("release_certificate", "Constancia de Liberación / Expediente Completo (CC)",
                i.Status == "released" || i.IsExpedientComplete || !string.IsNullOrWhiteSpace(i.ReleaseCertificateFolio),
                i.ReleaseCertificateFolio ?? "Pendiente de liberación")
        };

        var allowedActions = BuildAllowedActions(i);
        var progress = CalculateDocumentProgressPercentage(i, docs);

        InternshipAgreementSummary? agreementSummary = null;
        if (i.Agreement is not null)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var validity = !i.Agreement.IsActive
                ? "inactive"
                : today > i.Agreement.ExpirationDate
                    ? "expired"
                    : "valid";
            agreementSummary = new InternshipAgreementSummary(
                i.Agreement.Id,
                i.Agreement.AgreementNumber,
                validity);
        }

        return new InternshipDetailResponse(
            i.Id,
            new InternshipStudentSummary(
                student.Id,
                student.ControlNumber,
                fullName,
                student.Career?.Name ?? string.Empty,
                student.CurrentSemester,
                student.Shift,
                student.PhoneNumber,
                student.InstitutionalEmail,
                student.HasValidImss,
                !string.IsNullOrWhiteSpace(student.ImssValidityFileUrl)),
            new InternshipPeriodSummary(i.PeriodId, i.Period?.Code ?? string.Empty),
            i.Company is null ? null : new InternshipCompanySummary(i.Company.Id, i.Company.Name, i.Company.Sector),
            i.Supervisor is null ? null : new InternshipSupervisorSummary(i.Supervisor.Id, i.Supervisor.FullName, i.Supervisor.Position),
            agreementSummary,
            i.SemesterEnrolled,
            i.StartDate,
            i.EndDate,
            i.ScheduleDescription,
            i.RequiredHours,
            i.CompletedHours,
            progress,
            i.Status,
            i.CreditsAwarded,
            i.PresentationLetterFolio,
            i.PresentationLetterDate,
            i.AcceptanceLetterDate,
            i.TerminationLetterDate,
            i.OriginalTerminationLetterReceived,
            i.ReleaseCertificateFolio,
            i.ReleaseDate,
            i.ReleasedAt,
            i.IsExpedientComplete,
            i.SchoolServicesActSent,
            i.SchoolServicesActSentAt,
            i.SchoolServicesActFolio,
            i.Observations,
            i.CancellationReason,
            i.Evaluation is null ? null : ToEvaluationResponse(i.Evaluation),
            allowedActions,
            checklist,
            i.CreatedAt,
            i.UpdatedAt);
    }

    private IReadOnlyList<string> BuildAllowedActions(Internship i)
    {
        var actions = new List<string>();
        var isStaff = IsInternshipStaff();
        var isOwnerStudent = currentUser.IsInRole("student") && currentUser.Id.HasValue && i.Student?.UserId == currentUser.Id.Value;

        if (!isStaff && !isOwnerStudent)
        {
            return actions;
        }

        var isTerminal = TerminalStatuses.Contains(i.Status);

        if (!isTerminal)
        {
            actions.Add("register_acceptance");
            actions.Add("register_evaluation");
        }

        if (isOwnerStudent && i.Status == "requested")
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
                actions.Add("update_expedient");
                actions.Add("cancel");
                actions.Add("mark_non_compliant");
            }

            switch (i.Status)
            {
                case "requested":
                    actions.Add("approve");
                    break;
                case "approved" or "accepted":
                    actions.Add("activate");
                    actions.Add("complete");
                    break;
                case "active":
                    actions.Add("complete");
                    actions.Add("release");
                    break;
                case "completed_pending_review":
                    actions.Add("release");
                    break;
                case "released":
                    if (!i.SchoolServicesActSent)
                    {
                        actions.Add("send_school_act");
                    }
                    break;
            }
        }

        return actions.Distinct().ToList();
    }

    private static InternshipEvaluationResponse ToEvaluationResponse(InternshipEvaluation e) =>
        new(
            e.Id,
            e.ActivityName,
            e.EvaluationStartDate,
            e.EvaluationEndDate,
            [e.Criterion1, e.Criterion2, e.Criterion3, e.Criterion4, e.Criterion5, e.Criterion6, e.Criterion7],
            e.NumericalAverage,
            e.PerformanceLevel,
            e.IsCompetencyAchieved,
            e.EvaluatorSupervisorId,
            e.EvaluatorSignatureDate,
            e.Observations);
}
