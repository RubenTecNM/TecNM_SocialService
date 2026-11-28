using Microsoft.EntityFrameworkCore;
using TecNM.Api.Core.Entities;
using TecNM.Api.Core.Exceptions;
using TecNM.Api.Core.Http;

namespace TecNM.Api.Modules.Reports;

public sealed partial class ReportsService
{
    private sealed record AnalyticsProjectionRow(
        string TramiteType,
        long TramiteId,
        long StudentId,
        long PeriodId,
        string PeriodCode,
        string PeriodName,
        string SchoolYear,
        DateOnly PeriodStartDate,
        long CareerId,
        string CareerCode,
        string CareerName,
        bool CareerIsActive,
        int SemesterEnrolled,
        string? Gender,
        long? CompanyId,
        string? CompanyName,
        string? CompanyType,
        string? CompanySector,
        bool CompanyIsTecnmInternal,
        long? ActivityTypeId,
        string? ActivityTypeCode,
        string? ActivityTypeName,
        string Status
    );

    public async Task<AnalyticsReportResponse> GetAnalyticsAsync(
        AnalyticsFilterQuery filter,
        CancellationToken cancellationToken = default)
    {
        var normType = ValidateAnalyticsTramiteType(filter.TramiteType);
        var normScope = ValidateTimeScope(filter.TimeScope, filter.PeriodId, filter.SchoolYear);
        ValidateDimensionalFilters(filter);

        await using var tx = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.RepeatableRead, cancellationToken);

        var allPeriods = await dbContext.AcademicPeriods
            .AsNoTracking()
            .OrderBy(p => p.StartDate)
            .ThenBy(p => p.Id)
            .ToListAsync(cancellationToken);

        if (allPeriods.Count == 0)
        {
            throw new NotFoundException("academic_periods.not_found", "No existen periodos académicos registrados.");
        }

        var (selectedPeriods, resolvedPeriod, resolvedSchoolYear) = ResolveTimeSelection(
            allPeriods, normScope, filter.PeriodId, filter.SchoolYear);

        var selectedPeriodIds = selectedPeriods.Select(p => p.Id).ToHashSet();

        // Cargar proyecciones ligeras (sin entidades completas) con filtros dimensionales
        var allFilteredDimensionRows = await LoadAnalyticsProjectionsAsync(
            normType, periodIds: null, filter, cancellationToken);

        // 1. Filas del alcance temporal seleccionado (sin filtro de estatus aún, para embudo interactivo)
        var scopeRowsRaw = allFilteredDimensionRows
            .Where(r => selectedPeriodIds.Contains(r.PeriodId))
            .ToList();

        var scopeRowsDedupAllStatuses = DeduplicateByStudentProcess(scopeRowsRaw);

        // Embudo de estatus sobre el alcance temporal + filtros dimensionales (antes del filtro de estatus)
        var byStatus = ReportMetricRules.AllStatuses
            .Select(st =>
            {
                var matches = scopeRowsDedupAllStatuses.Where(r => r.Status == st).ToList();
                return new StatusCountRow(
                    st,
                    matches.Count,
                    matches.Count(r => r.TramiteType == "social_service"),
                    matches.Count(r => r.TramiteType == "internship"));
            })
            .ToList();

        // 2. Aplicar filtro de universo / estatus (default: in_progress)
        var allowedStatuses = ReportMetricRules.ResolveAllowedStatuses(filter.StatusGroup, filter.Status);
        var activeRaw = scopeRowsRaw.Where(r => allowedStatuses.Contains(r.Status)).ToList();
        var activeDedup = DeduplicateByStudentProcess(activeRaw);

        // 3. Comparativa contra periodo o ciclo anterior (si se solicitó)
        ReportPeriodSummary? comparePeriodSummary = null;
        string? compareSchoolYear = null;
        AnalyticsComparisonDelta? comparisonDelta = null;

        if (normScope == "period" && filter.ComparePeriodId.HasValue)
        {
            var cmpPeriod = allPeriods.SingleOrDefault(p => p.Id == filter.ComparePeriodId.Value);
            if (cmpPeriod is not null)
            {
                comparePeriodSummary = new ReportPeriodSummary(cmpPeriod.Id, cmpPeriod.Code, cmpPeriod.Name);
                var prevRows = DeduplicateByStudentProcess(
                    allFilteredDimensionRows.Where(r => r.PeriodId == cmpPeriod.Id && allowedStatuses.Contains(r.Status)));
                comparisonDelta = BuildComparisonDelta(cmpPeriod.Code, prevRows, activeDedup);
            }
        }
        else if (normScope == "school_year" && !string.IsNullOrWhiteSpace(filter.CompareSchoolYear))
        {
            var normCmpSy = filter.CompareSchoolYear.Trim();
            var cmpPeriodIds = allPeriods.Where(p => p.SchoolYear == normCmpSy).Select(p => p.Id).ToHashSet();
            if (cmpPeriodIds.Count > 0)
            {
                compareSchoolYear = normCmpSy;
                var prevRows = DeduplicateByStudentProcess(
                    allFilteredDimensionRows.Where(r => cmpPeriodIds.Contains(r.PeriodId) && allowedStatuses.Contains(r.Status)));
                comparisonDelta = BuildComparisonDelta($"Ciclo {normCmpSy}", prevRows, activeDedup);
            }
        }

        // 4. Construir resumen y desgloses (cuadratura exacta sobre activeDedup)
        var uniqueStudentsTotal = activeDedup.Select(r => r.StudentId).Distinct().Count();
        var ssCount = activeDedup.Count(r => r.TramiteType == "social_service");
        var ppCount = activeDedup.Count(r => r.TramiteType == "internship");
        var inProgressCount = activeDedup.Count(r => ReportMetricRules.InProgressStatuses.Contains(r.Status));
        var activeCount = activeDedup.Count(r => r.Status == "active");
        var releasedCount = activeDedup.Count(r => r.Status == "released");
        var pncCount = activeDedup.Count(r => r.Status == "non_compliant");
        var cancelledCount = activeDedup.Count(r => r.Status == "cancelled");
        var releaseRate = activeDedup.Count > 0 ? Math.Round(releasedCount * 100m / activeDedup.Count, 2) : 0m;

        var distinctCompanies = activeDedup
            .Where(r => r.CompanyId.HasValue)
            .Select(r => r.CompanyId!.Value)
            .Distinct()
            .Count();

        var unassignedCompanyCount = activeDedup.Count(r => !r.CompanyId.HasValue);
        var unclassifiedActivityCount = activeDedup.Count(r => !r.ActivityTypeId.HasValue);

        var genderBreakdown = new GenderBreakdown(
            activeDedup.Count(r => r.Gender == "M"),
            activeDedup.Count(r => r.Gender == "F"),
            activeDedup.Count(r => r.Gender is not ("M" or "F")));

        var summary = new AnalyticsSummary(
            uniqueStudentsTotal,
            activeRaw.Count,
            ssCount,
            ppCount,
            inProgressCount,
            activeCount,
            releasedCount,
            pncCount,
            cancelledCount,
            releaseRate,
            distinctCompanies,
            unassignedCompanyCount,
            unclassifiedActivityCount,
            genderBreakdown,
            comparisonDelta);

        var byCareer = activeDedup
            .GroupBy(r => new { r.CareerId, r.CareerCode, r.CareerName, r.CareerIsActive })
            .Select(g => new CareerAnalyticsRow(
                g.Key.CareerId,
                g.Key.CareerCode,
                g.Key.CareerName,
                g.Key.CareerIsActive,
                g.Count(),
                g.Count(r => r.Gender == "M"),
                g.Count(r => r.Gender == "F"),
                g.Count(r => r.Gender is not ("M" or "F")),
                g.Count(r => ReportMetricRules.InProgressStatuses.Contains(r.Status)),
                g.Count(r => r.Status == "released"),
                g.Count(r => r.Status == "non_compliant"),
                g.Count(r => r.TramiteType == "social_service"),
                g.Count(r => r.TramiteType == "internship")))
            .OrderByDescending(c => c.Total)
            .ThenBy(c => c.CareerCode)
            .ToList();

        var bySemester = activeDedup
            .GroupBy(r => r.SemesterEnrolled)
            .Select(g => new SemesterAnalyticsRow(
                g.Key,
                g.Count(),
                g.Count(r => r.Gender == "M"),
                g.Count(r => r.Gender == "F"),
                g.Count(r => r.Gender is not ("M" or "F")),
                g.Count(r => r.TramiteType == "social_service"),
                g.Count(r => r.TramiteType == "internship")))
            .OrderBy(s => s.Semester)
            .ToList();

        var totalDedupRows = activeDedup.Count;
        var byActivityType = activeDedup
            .GroupBy(r => new
            {
                r.ActivityTypeId,
                Code = r.ActivityTypeCode ?? "UNCLASSIFIED",
                Name = r.ActivityTypeName ?? "Sin clasificar"
            })
            .Select(g => new ActivityTypeAnalyticsRow(
                g.Key.ActivityTypeId,
                g.Key.Code,
                g.Key.Name,
                g.Count(),
                g.Count(r => r.Gender == "M"),
                g.Count(r => r.Gender == "F"),
                g.Count(r => r.Gender is not ("M" or "F")),
                g.Count(r => r.TramiteType == "social_service"),
                g.Count(r => r.TramiteType == "internship"),
                totalDedupRows > 0 ? Math.Round(g.Count() * 100m / totalDedupRows, 1) : 0m))
            .OrderBy(a => a.ActivityTypeId is null ? 1 : 0)
            .ThenByDescending(a => a.Total)
            .ThenBy(a => a.Name)
            .ToList();

        var topCompanies = BuildCompanyRows(activeDedup, activeRaw)
            .OrderBy(c => c.CompanyId is null ? 1 : 0)
            .ThenByDescending(c => c.Total)
            .ThenBy(c => c.CompanyName)
            .Take(10)
            .ToList();

        // 5. Serie histórica: por Periodo (en modo period) o por Ciclo Escolar (en modo school_year / all)
        var trendSource = allFilteredDimensionRows.Where(r => allowedStatuses.Contains(r.Status)).ToList();
        IReadOnlyList<TrendPointRow> trend;
        if (normScope == "period")
        {
            trend = allPeriods
                .Select(p =>
                {
                    var bucket = DeduplicateByStudentProcess(trendSource.Where(r => r.PeriodId == p.Id));
                    return new TrendPointRow(
                        p.Id.ToString(),
                        p.Code,
                        p.Name,
                        bucket.Count,
                        bucket.Count(r => r.TramiteType == "social_service"),
                        bucket.Count(r => r.TramiteType == "internship"),
                        bucket.Count(r => ReportMetricRules.InProgressStatuses.Contains(r.Status)),
                        bucket.Count(r => r.Status == "released"));
                })
                .ToList();
        }
        else
        {
            trend = allPeriods
                .GroupBy(p => p.SchoolYear)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g =>
                {
                    var pIds = g.Select(p => p.Id).ToHashSet();
                    var bucket = DeduplicateByStudentProcess(trendSource.Where(r => pIds.Contains(r.PeriodId)));
                    var codes = string.Join(" + ", g.OrderBy(p => p.StartDate).Select(p => p.Code));
                    return new TrendPointRow(
                        g.Key,
                        g.Key,
                        codes,
                        bucket.Count,
                        bucket.Count(r => r.TramiteType == "social_service"),
                        bucket.Count(r => r.TramiteType == "internship"),
                        bucket.Count(r => ReportMetricRules.InProgressStatuses.Contains(r.Status)),
                        bucket.Count(r => r.Status == "released"));
                })
                .ToList();
        }

        await tx.CommitAsync(cancellationToken);

        var scopeSummary = new AnalyticsScopeSummary(
            normType,
            normScope,
            resolvedPeriod is null ? null : new ReportPeriodSummary(resolvedPeriod.Id, resolvedPeriod.Code, resolvedPeriod.Name),
            resolvedSchoolYear,
            comparePeriodSummary,
            compareSchoolYear,
            filter.StatusGroup.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(filter.Status) ? null : filter.Status.Trim().ToLowerInvariant(),
            selectedPeriods.OrderBy(p => p.StartDate).Select(p => p.Code).ToList());

        return new AnalyticsReportResponse(
            scopeSummary,
            summary,
            byStatus,
            byCareer,
            bySemester,
            byActivityType,
            topCompanies,
            trend);
    }

    public async Task<PagedResponse<CompanyAnalyticsRow>> GetAnalyticsCompaniesAsync(
        AnalyticsFilterQuery filter,
        string? search,
        string? sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var normType = ValidateAnalyticsTramiteType(filter.TramiteType);
        var normScope = ValidateTimeScope(filter.TimeScope, filter.PeriodId, filter.SchoolYear);
        ValidateDimensionalFilters(filter);

        await using var tx = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.RepeatableRead, cancellationToken);

        var allPeriods = await dbContext.AcademicPeriods
            .AsNoTracking()
            .OrderBy(p => p.StartDate)
            .ToListAsync(cancellationToken);

        var (selectedPeriods, _, _) = ResolveTimeSelection(allPeriods, normScope, filter.PeriodId, filter.SchoolYear);
        var selectedPeriodIds = selectedPeriods.Select(p => p.Id).ToHashSet();

        var allowedStatuses = ReportMetricRules.ResolveAllowedStatuses(filter.StatusGroup, filter.Status);
        var rawRows = (await LoadAnalyticsProjectionsAsync(normType, selectedPeriodIds, filter, cancellationToken))
            .Where(r => allowedStatuses.Contains(r.Status))
            .ToList();

        var dedupRows = DeduplicateByStudentProcess(rawRows);
        var companyRows = BuildCompanyRows(dedupRows, rawRows);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            companyRows = companyRows
                .Where(c => c.CompanyName.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        companyRows = (sort?.Trim().ToLowerInvariant()) switch
        {
            "active_desc" => companyRows.OrderByDescending(c => c.ActiveCount).ThenByDescending(c => c.Total).ThenBy(c => c.CompanyName).ToList(),
            "released_desc" => companyRows.OrderByDescending(c => c.ReleasedCount).ThenByDescending(c => c.Total).ThenBy(c => c.CompanyName).ToList(),
            "name_asc" => companyRows.OrderBy(c => c.CompanyId is null ? 1 : 0).ThenBy(c => c.CompanyName).ToList(),
            _ => companyRows.OrderBy(c => c.CompanyId is null ? 1 : 0).ThenByDescending(c => c.Total).ThenBy(c => c.CompanyName).ToList()
        };

        await tx.CommitAsync(cancellationToken);

        var totalItems = companyRows.Count;
        var totalPages = totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var items = companyRows.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResponse<CompanyAnalyticsRow>(items, page, pageSize, totalItems, totalPages);
    }

    private async Task<List<AnalyticsProjectionRow>> LoadAnalyticsProjectionsAsync(
        string tramiteType,
        HashSet<long>? periodIds,
        AnalyticsFilterQuery filter,
        CancellationToken cancellationToken)
    {
        var results = new List<AnalyticsProjectionRow>();

        if (tramiteType is "social_service" or "both")
        {
            var ssQuery = dbContext.SocialServices
                .AsNoTracking()
                .Where(s => s.Student != null && s.Period != null && s.Student.Career != null);

            if (periodIds is not null)
                ssQuery = ssQuery.Where(s => periodIds.Contains(s.PeriodId));
            if (filter.CareerId.HasValue)
                ssQuery = ssQuery.Where(s => s.Student!.CareerId == filter.CareerId.Value);
            if (filter.Semester.HasValue)
                ssQuery = ssQuery.Where(s => s.SemesterEnrolled == filter.Semester.Value);
            if (filter.CompanyId.HasValue)
                ssQuery = filter.CompanyId.Value == 0
                    ? ssQuery.Where(_ => false) // SS siempre tiene empresa asignada
                    : ssQuery.Where(s => s.CompanyId == filter.CompanyId.Value);
            if (!string.IsNullOrWhiteSpace(filter.CompanyType))
            {
                var ct = filter.CompanyType.Trim().ToLowerInvariant();
                ssQuery = ssQuery.Where(s => s.Company != null && s.Company.CompanyType == ct);
            }
            if (!string.IsNullOrWhiteSpace(filter.Sector))
            {
                var sec = filter.Sector.Trim().ToLowerInvariant();
                ssQuery = ssQuery.Where(s => s.Company != null && s.Company.Sector == sec);
            }
            if (filter.ActivityTypeId.HasValue)
                ssQuery = filter.ActivityTypeId.Value == 0
                    ? ssQuery.Where(s => s.ActivityTypeId == null)
                    : ssQuery.Where(s => s.ActivityTypeId == filter.ActivityTypeId.Value);
            if (!string.IsNullOrWhiteSpace(filter.Gender))
            {
                var g = filter.Gender.Trim().ToUpperInvariant();
                ssQuery = g == "UNSPECIFIED"
                    ? ssQuery.Where(s => s.Student!.Gender == null)
                    : ssQuery.Where(s => s.Student!.Gender == g);
            }

            var ssRows = await ssQuery
                .Select(s => new AnalyticsProjectionRow(
                    "social_service",
                    s.Id,
                    s.StudentId,
                    s.PeriodId,
                    s.Period!.Code,
                    s.Period.Name,
                    s.Period.SchoolYear,
                    s.Period.StartDate,
                    s.Student!.CareerId,
                    s.Student.Career!.Code,
                    s.Student.Career.Name,
                    s.Student.Career.IsActive,
                    s.SemesterEnrolled,
                    s.Student.Gender,
                    s.CompanyId,
                    s.Company != null ? s.Company.Name : null,
                    s.Company != null ? s.Company.CompanyType : null,
                    s.Company != null ? s.Company.Sector : null,
                    s.Company != null && s.Company.IsTecnmInternal,
                    s.ActivityTypeId,
                    s.ActivityType != null ? s.ActivityType.Code : null,
                    s.ActivityType != null ? s.ActivityType.Name : null,
                    s.Status))
                .ToListAsync(cancellationToken);

            results.AddRange(ssRows);
        }

        if (tramiteType is "internship" or "both")
        {
            var ppQuery = dbContext.Internships
                .AsNoTracking()
                .Where(i => i.Student != null && i.Period != null && i.Student.Career != null);

            if (periodIds is not null)
                ppQuery = ppQuery.Where(i => periodIds.Contains(i.PeriodId));
            if (filter.CareerId.HasValue)
                ppQuery = ppQuery.Where(i => i.Student!.CareerId == filter.CareerId.Value);
            if (filter.Semester.HasValue)
                ppQuery = ppQuery.Where(i => i.SemesterEnrolled == filter.Semester.Value);
            if (filter.CompanyId.HasValue)
                ppQuery = filter.CompanyId.Value == 0
                    ? ppQuery.Where(i => i.CompanyId == null)
                    : ppQuery.Where(i => i.CompanyId == filter.CompanyId.Value);
            if (!string.IsNullOrWhiteSpace(filter.CompanyType))
            {
                var ct = filter.CompanyType.Trim().ToLowerInvariant();
                ppQuery = ppQuery.Where(i => i.Company != null && i.Company.CompanyType == ct);
            }
            if (!string.IsNullOrWhiteSpace(filter.Sector))
            {
                var sec = filter.Sector.Trim().ToLowerInvariant();
                ppQuery = ppQuery.Where(i => i.Company != null && i.Company.Sector == sec);
            }
            if (filter.ActivityTypeId.HasValue)
                ppQuery = filter.ActivityTypeId.Value == 0
                    ? ppQuery.Where(i => i.ActivityTypeId == null)
                    : ppQuery.Where(i => i.ActivityTypeId == filter.ActivityTypeId.Value);
            if (!string.IsNullOrWhiteSpace(filter.Gender))
            {
                var g = filter.Gender.Trim().ToUpperInvariant();
                ppQuery = g == "UNSPECIFIED"
                    ? ppQuery.Where(i => i.Student!.Gender == null)
                    : ppQuery.Where(i => i.Student!.Gender == g);
            }

            var ppRows = await ppQuery
                .Select(i => new AnalyticsProjectionRow(
                    "internship",
                    i.Id,
                    i.StudentId,
                    i.PeriodId,
                    i.Period!.Code,
                    i.Period.Name,
                    i.Period.SchoolYear,
                    i.Period.StartDate,
                    i.Student!.CareerId,
                    i.Student.Career!.Code,
                    i.Student.Career.Name,
                    i.Student.Career.IsActive,
                    i.SemesterEnrolled,
                    i.Student.Gender,
                    i.CompanyId,
                    i.Company != null ? i.Company.Name : null,
                    i.Company != null ? i.Company.CompanyType : null,
                    i.Company != null ? i.Company.Sector : null,
                    i.Company != null && i.Company.IsTecnmInternal,
                    i.ActivityTypeId,
                    i.ActivityType != null ? i.ActivityType.Code : null,
                    i.ActivityType != null ? i.ActivityType.Name : null,
                    i.Status))
                .ToListAsync(cancellationToken);

            results.AddRange(ppRows);
        }

        return results;
    }

    private static List<AnalyticsProjectionRow> DeduplicateByStudentProcess(IEnumerable<AnalyticsProjectionRow> rows) =>
        rows
            .GroupBy(r => (r.TramiteType, r.StudentId))
            .Select(g => g
                .OrderByDescending(r => ReportMetricRules.StatusPriority(r.Status))
                .ThenByDescending(r => r.PeriodStartDate)
                .ThenByDescending(r => r.TramiteId)
                .First())
            .ToList();

    private static List<CompanyAnalyticsRow> BuildCompanyRows(
        List<AnalyticsProjectionRow> dedupRows,
        List<AnalyticsProjectionRow> rawRows)
    {
        var projectCounts = rawRows
            .GroupBy(r => r.CompanyId)
            .ToDictionary(g => g.Key ?? 0L, g => g.Count());

        return dedupRows
            .GroupBy(r => new
            {
                r.CompanyId,
                CompanyName = r.CompanyName ?? "Sin empresa asignada (pendiente de CA)",
                r.CompanyType,
                r.CompanySector,
                r.CompanyIsTecnmInternal
            })
            .Select(g =>
            {
                var topAct = g
                    .Where(r => !string.IsNullOrWhiteSpace(r.ActivityTypeName))
                    .GroupBy(r => r.ActivityTypeName!)
                    .OrderByDescending(a => a.Count())
                    .Select(a => a.Key)
                    .FirstOrDefault();

                return new CompanyAnalyticsRow(
                    g.Key.CompanyId,
                    g.Key.CompanyName,
                    g.Key.CompanyType,
                    g.Key.CompanySector,
                    g.Key.CompanyIsTecnmInternal,
                    g.Count(),
                    g.Count(r => r.Status == "active"),
                    g.Count(r => r.Status == "released"),
                    projectCounts.GetValueOrDefault(g.Key.CompanyId ?? 0L, g.Count()),
                    g.Count(r => r.Gender == "M"),
                    g.Count(r => r.Gender == "F"),
                    g.Count(r => r.Gender is not ("M" or "F")),
                    topAct);
            })
            .ToList();
    }

    private static AnalyticsComparisonDelta BuildComparisonDelta(
        string previousLabel,
        List<AnalyticsProjectionRow> prevDedup,
        List<AnalyticsProjectionRow> currentDedup)
    {
        var prevUnique = prevDedup.Select(r => r.StudentId).Distinct().Count();
        var currUnique = currentDedup.Select(r => r.StudentId).Distinct().Count();
        var delta = currUnique - prevUnique;
        decimal? deltaPct = prevUnique > 0 ? Math.Round(delta * 100m / prevUnique, 1) : null;
        var prevReleased = prevDedup.Count(r => r.Status == "released");
        var prevRate = prevDedup.Count > 0 ? Math.Round(prevReleased * 100m / prevDedup.Count, 2) : 0m;

        return new AnalyticsComparisonDelta(previousLabel, prevUnique, delta, deltaPct, prevReleased, prevRate);
    }

    private static (List<AcademicPeriod> Periods, AcademicPeriod? ResolvedPeriod, string? ResolvedSchoolYear) ResolveTimeSelection(
        List<AcademicPeriod> allPeriods,
        string timeScope,
        long? periodId,
        string? schoolYear)
    {
        if (timeScope == "all" || (timeScope == "period" && periodId == 0))
        {
            return (allPeriods, null, null);
        }

        if (timeScope == "school_year")
        {
            var targetSy = !string.IsNullOrWhiteSpace(schoolYear)
                ? schoolYear.Trim()
                : (allPeriods.FirstOrDefault(p => p.IsActive) ?? allPeriods.Last()).SchoolYear;

            var matched = allPeriods.Where(p => p.SchoolYear == targetSy).ToList();
            if (matched.Count == 0)
            {
                throw new NotFoundException("academic_periods.school_year_not_found", $"Ciclo escolar '{targetSy}' no encontrado.");
            }

            return (matched, null, targetSy);
        }

        AcademicPeriod resolved;
        if (periodId.HasValue && periodId.Value > 0)
        {
            resolved = allPeriods.SingleOrDefault(p => p.Id == periodId.Value)
                ?? throw new NotFoundException("academic_periods.not_found", $"Periodo académico #{periodId} no encontrado.");
        }
        else
        {
            resolved = allPeriods.OrderByDescending(p => p.IsActive).ThenByDescending(p => p.StartDate).First();
        }

        return ([resolved], resolved, resolved.SchoolYear);
    }

    private static string ValidateAnalyticsTramiteType(string? tramiteType)
    {
        var norm = tramiteType?.Trim().ToLowerInvariant() ?? "social_service";
        if (norm is not ("social_service" or "internship" or "both"))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["tramiteType"] = ["El parámetro tramiteType debe ser 'social_service', 'internship' o 'both'."]
            });
        }
        return norm;
    }

    private static string ValidateTimeScope(string? timeScope, long? periodId, string? schoolYear)
    {
        if (!string.IsNullOrWhiteSpace(schoolYear) && string.Equals(timeScope, "period", StringComparison.OrdinalIgnoreCase) && !periodId.HasValue)
        {
            return "school_year";
        }
        var norm = timeScope?.Trim().ToLowerInvariant() ?? "period";
        if (norm is not ("period" or "school_year" or "all"))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["timeScope"] = ["El parámetro timeScope debe ser 'period', 'school_year' o 'all'."]
            });
        }
        return norm;
    }

    private static void ValidateDimensionalFilters(AnalyticsFilterQuery filter)
    {
        var errors = new Dictionary<string, string[]>();

        if (filter.Semester.HasValue && (filter.Semester.Value < 1 || filter.Semester.Value > 15))
        {
            errors["semester"] = ["El semestre debe estar entre 1 y 15."];
        }

        if (!string.IsNullOrWhiteSpace(filter.Gender))
        {
            var g = filter.Gender.Trim().ToUpperInvariant();
            if (g is not ("M" or "F" or "UNSPECIFIED"))
            {
                errors["gender"] = ["El sexo debe ser 'M', 'F' o 'unspecified'."];
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.CompanyType)
            && !CompanyTypes.All.Contains(filter.CompanyType.Trim().ToLowerInvariant()))
        {
            errors["companyType"] = ["Tipo de empresa inválido."];
        }

        if (!string.IsNullOrWhiteSpace(filter.Sector)
            && !AllSectors.Contains(filter.Sector.Trim().ToLowerInvariant()))
        {
            errors["sector"] = ["Sector inválido."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }
}
