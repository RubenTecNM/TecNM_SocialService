using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TecNM.Api.Core.Entities;
using TecNM.Api.Core.Exceptions;
using TecNM.Api.Core.Http;
using TecNM.Api.Core.Security;
using TecNM.Api.Data;

namespace TecNM.Api.Modules.Quality;

public sealed class QualityService(
    TecNMDbContext dbContext,
    ICurrentUser currentUser)
{
    private static readonly HashSet<string> ValidTramiteTypes =
    [
        "social_service",
        "internship"
    ];

    private static readonly HashSet<string> ValidCategories =
    [
        "desertion",
        "delayed_reports",
        "company_dismissal",
        "insufficient_grade",
        "invalid_documents",
        "other"
    ];

    private static readonly HashSet<string> ValidStatuses =
    [
        "open",
        "under_review",
        "resolved",
        "cancelled"
    ];

    public async Task<IReadOnlyList<StatusLogFullResponse>> GetStatusLogsAsync(
        string tramiteType,
        long tramiteId,
        CancellationToken cancellationToken = default)
    {
        var normType = tramiteType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!ValidTramiteTypes.Contains(normType) || tramiteId <= 0)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["tramiteType"] = ["Se requiere tramiteType ('social_service' o 'internship') y tramiteId válidos."]
            });
        }

        await EnsureCanReadTramiteLogsAsync(normType, tramiteId, cancellationToken);

        var logs = await dbContext.TramiteStatusLogs
            .Include(l => l.ChangedByUser)
            .AsNoTracking()
            .Where(l => l.TramiteType == normType && l.TramiteId == tramiteId)
            .OrderBy(l => l.CreatedAt)
            .ThenBy(l => l.Id)
            .ToListAsync(cancellationToken);

        return logs
            .Select(l => new StatusLogFullResponse(
                l.Id,
                l.TramiteType,
                l.TramiteId,
                l.PreviousStatus,
                l.NewStatus,
                l.Reason,
                l.ChangedBy,
                l.ChangedByUser is null
                    ? "Sistema"
                    : string.Join(" ", new[] { l.ChangedByUser.FirstName, l.ChangedByUser.FirstSurname, l.ChangedByUser.SecondSurname }
                        .Where(x => !string.IsNullOrWhiteSpace(x))),
                l.CreatedAt))
            .ToList();
    }

    public async Task<PagedResponse<NonConformanceResponse>> GetPagedNonConformancesAsync(
        int page,
        int pageSize,
        long? periodId,
        string? tramiteType,
        string? status,
        string? category,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedSize = Math.Clamp(pageSize, 1, 100);

        var query = BuildBaseQuery().AsNoTracking();

        if (periodId.HasValue)
        {
            query = query.Where(n => n.PeriodId == periodId.Value);
        }

        if (!string.IsNullOrWhiteSpace(tramiteType))
        {
            var normType = tramiteType.Trim().ToLowerInvariant();
            query = query.Where(n => n.TramiteType == normType);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normStatus = status.Trim().ToLowerInvariant();
            query = query.Where(n => n.Status == normStatus);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var normCategory = category.Trim().ToLowerInvariant();
            query = query.Where(n => n.Category == normCategory);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            if (term.Length > 100)
            {
                term = term[..100];
            }

            var pattern = $"%{term}%";
            query = query.Where(n =>
                EF.Functions.ILike(n.IncidentCode, pattern) ||
                EF.Functions.ILike(n.Description, pattern) ||
                (n.Student != null && (
                    EF.Functions.ILike(n.Student.ControlNumber, pattern) ||
                    EF.Functions.ILike(n.Student.FirstName, pattern) ||
                    EF.Functions.ILike(n.Student.FirstSurname, pattern) ||
                    (n.Student.SecondSurname != null && EF.Functions.ILike(n.Student.SecondSurname, pattern)))));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalItems / (double)normalizedSize);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Skip((normalizedPage - 1) * normalizedSize)
            .Take(normalizedSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<NonConformanceResponse>(
            items.Select(ToResponse).ToList(),
            normalizedPage,
            normalizedSize,
            totalItems,
            totalPages);
    }

    public async Task<NonConformanceResponse> GetNonConformanceByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await BuildBaseQuery()
            .AsNoTracking()
            .SingleOrDefaultAsync(n => n.Id == id, cancellationToken)
            ?? throw new NotFoundException("non_conformances.not_found", $"Incidencia PNC #{id} no encontrada.");

        return ToResponse(entity);
    }

    public async Task<NonConformanceResponse> CreateNonConformanceAsync(
        CreateNonConformanceRequest request,
        CancellationToken cancellationToken = default)
    {
        var normType = request.TramiteType?.Trim().ToLowerInvariant() ?? string.Empty;
        var normCat = request.Category?.Trim().ToLowerInvariant() ?? string.Empty;

        var errors = new Dictionary<string, string[]>();
        if (!ValidTramiteTypes.Contains(normType))
        {
            errors["tramiteType"] = ["El tipo de trámite debe ser 'social_service' o 'internship'."];
        }

        if (!ValidCategories.Contains(normCat))
        {
            errors["category"] = [$"Categoría inválida. Opciones: {string.Join(", ", ValidCategories)}."];
        }

        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length > 4000)
        {
            errors["description"] = ["La descripción es obligatoria (1 a 4000 caracteres)."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        EnsureCanWriteForTramiteType(normType);

        long studentId;
        long periodId;

        if (normType == "social_service")
        {
            var ss = await dbContext.SocialServices
                .AsNoTracking()
                .Where(s => s.Id == request.TramiteId)
                .Select(s => new { s.Id, s.StudentId, s.PeriodId })
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("social_services.not_found", $"Trámite de Servicio Social #{request.TramiteId} no encontrado.");

            studentId = ss.StudentId;
            periodId = ss.PeriodId;
        }
        else
        {
            var pp = await dbContext.Internships
                .AsNoTracking()
                .Where(i => i.Id == request.TramiteId)
                .Select(i => new { i.Id, i.StudentId, i.PeriodId })
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("internships.not_found", $"Trámite de Prácticas Profesionales #{request.TramiteId} no encontrado.");

            studentId = pp.StudentId;
            periodId = pp.PeriodId;
        }

        var year = DateTime.UtcNow.Year;
        var prefix = normType == "social_service" ? $"PNC-SS-{year}-" : $"PNC-PP-{year}-";
        var countThisYear = await dbContext.NonConformances
            .IgnoreQueryFilters()
            .CountAsync(n => n.TramiteType == normType && n.CreatedAt.Year == year, cancellationToken);

        var incidentCode = $"{prefix}{(countThisYear + 1):D3}";

        var entity = new NonConformance
        {
            IncidentCode = incidentCode,
            TramiteType = normType,
            TramiteId = request.TramiteId,
            StudentId = studentId,
            PeriodId = periodId,
            Category = normCat,
            Description = request.Description.Trim(),
            Status = "open",
            ActionPlan = string.IsNullOrWhiteSpace(request.ActionPlan) ? null : request.ActionPlan.Trim()
        };

        dbContext.NonConformances.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetNonConformanceByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<NonConformanceResponse> UpdateNonConformanceAsync(
        long id,
        UpdateNonConformanceRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.NonConformances
            .SingleOrDefaultAsync(n => n.Id == id, cancellationToken)
            ?? throw new NotFoundException("non_conformances.not_found", $"Incidencia PNC #{id} no encontrada.");

        EnsureCanWriteForTramiteType(entity.TramiteType);

        if (entity.Status is not ("open" or "under_review"))
        {
            throw new ConflictException(
                "non_conformances.invalid_transition",
                $"Únicamente se pueden editar PNC en estado 'open' o 'under_review' (actual: '{entity.Status}').");
        }

        var normCat = request.Category?.Trim().ToLowerInvariant() ?? string.Empty;
        var errors = new Dictionary<string, string[]>();

        if (!ValidCategories.Contains(normCat))
        {
            errors["category"] = [$"Categoría inválida. Opciones: {string.Join(", ", ValidCategories)}."];
        }

        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length > 4000)
        {
            errors["description"] = ["La descripción es obligatoria (1 a 4000 caracteres)."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        entity.Category = normCat;
        entity.Description = request.Description.Trim();
        entity.ActionPlan = string.IsNullOrWhiteSpace(request.ActionPlan) ? null : request.ActionPlan.Trim();

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetNonConformanceByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<NonConformanceResponse> UpdateNonConformanceStatusAsync(
        long id,
        UpdateNonConformanceStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.NonConformances
            .SingleOrDefaultAsync(n => n.Id == id, cancellationToken)
            ?? throw new NotFoundException("non_conformances.not_found", $"Incidencia PNC #{id} no encontrada.");

        EnsureCanWriteForTramiteType(entity.TramiteType);

        var targetStatus = request.Status?.Trim().ToLowerInvariant() ?? string.Empty;
        if (targetStatus is not ("under_review" or "resolved" or "cancelled"))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["status"] = ["El nuevo estado debe ser 'under_review', 'resolved' o 'cancelled'."]
            });
        }

        var validTransition = (entity.Status, targetStatus) switch
        {
            ("open", "under_review" or "resolved" or "cancelled") => true,
            ("under_review", "resolved" or "cancelled") => true,
            _ => false
        };

        if (!validTransition)
        {
            throw new ConflictException(
                "non_conformances.invalid_transition",
                $"Transición de estado inválida: '{entity.Status}' -> '{targetStatus}'.");
        }

        var actionPlan = string.IsNullOrWhiteSpace(request.ActionPlan)
            ? entity.ActionPlan
            : request.ActionPlan.Trim();

        if (targetStatus == "resolved" && string.IsNullOrWhiteSpace(actionPlan))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["actionPlan"] = ["El plan de acción correctiva es obligatorio para resolver el Producto No Conforme."]
            });
        }

        entity.Status = targetStatus;
        entity.ActionPlan = actionPlan;

        if (targetStatus == "resolved")
        {
            entity.ResolvedAt = DateTimeOffset.UtcNow;
            entity.ResolvedBy = currentUser.Id;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetNonConformanceByIdAsync(entity.Id, cancellationToken);
    }

    private IQueryable<NonConformance> BuildBaseQuery() =>
        dbContext.NonConformances
            .Include(n => n.Student)
            .Include(n => n.Period)
            .Include(n => n.Resolver);

    private void EnsureCanWriteForTramiteType(string tramiteType)
    {
        if (currentUser.IsInRole("admin"))
        {
            return;
        }

        if (tramiteType == "social_service" && currentUser.IsInRole("social_service_officer"))
        {
            return;
        }

        if (tramiteType == "internship" && currentUser.IsInRole("internship_coordinator"))
        {
            return;
        }

        throw new AppException(
            StatusCodes.Status403Forbidden,
            "auth.forbidden",
            $"No tiene permisos para gestionar Producto No Conforme del trámite '{tramiteType}'.",
            "Acceso denegado");
    }

    private async Task EnsureCanReadTramiteLogsAsync(
        string tramiteType,
        long tramiteId,
        CancellationToken cancellationToken)
    {
        var isStaffOrViewer = currentUser.IsInRole("admin")
                              || currentUser.IsInRole("social_service_officer")
                              || currentUser.IsInRole("internship_coordinator")
                              || currentUser.IsInRole("department_head");

        if (tramiteType == "social_service")
        {
            var ss = await dbContext.SocialServices
                .Include(s => s.Student)
                .AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == tramiteId, cancellationToken)
                ?? throw new NotFoundException("social_services.not_found", $"Trámite de Servicio Social #{tramiteId} no encontrado.");

            if (!isStaffOrViewer && (!currentUser.Id.HasValue || ss.Student?.UserId != currentUser.Id.Value))
            {
                throw new NotFoundException("social_services.not_found", $"Trámite de Servicio Social #{tramiteId} no encontrado.");
            }
        }
        else
        {
            var pp = await dbContext.Internships
                .Include(i => i.Student)
                .AsNoTracking()
                .SingleOrDefaultAsync(i => i.Id == tramiteId, cancellationToken)
                ?? throw new NotFoundException("internships.not_found", $"Trámite de Prácticas Profesionales #{tramiteId} no encontrado.");

            if (!isStaffOrViewer && (!currentUser.Id.HasValue || pp.Student?.UserId != currentUser.Id.Value))
            {
                throw new NotFoundException("internships.not_found", $"Trámite de Prácticas Profesionales #{tramiteId} no encontrado.");
            }
        }
    }

    private static NonConformanceResponse ToResponse(NonConformance n)
    {
        var student = n.Student;
        var fullName = student is null
            ? "Sin nombre"
            : string.Join(" ", new[] { student.FirstName, student.FirstSurname, student.SecondSurname }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

        var resolverName = n.Resolver is null
            ? null
            : string.Join(" ", new[] { n.Resolver.FirstName, n.Resolver.FirstSurname, n.Resolver.SecondSurname }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

        return new NonConformanceResponse(
            n.Id,
            n.IncidentCode,
            n.TramiteType,
            n.TramiteId,
            n.StudentId,
            student?.ControlNumber ?? string.Empty,
            fullName,
            n.PeriodId,
            n.Period?.Code ?? string.Empty,
            n.Category,
            n.Description,
            n.Status,
            n.ActionPlan,
            n.ResolvedAt,
            resolverName,
            n.CreatedAt,
            n.UpdatedAt);
    }
}

