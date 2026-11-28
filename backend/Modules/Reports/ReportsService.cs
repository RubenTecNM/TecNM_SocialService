using Microsoft.EntityFrameworkCore;
using TecNM.Api.Core.Entities;
using TecNM.Api.Core.Exceptions;
using TecNM.Api.Core.Security;
using TecNM.Api.Data;
using TecNM.Api.Modules.Expedients;

namespace TecNM.Api.Modules.Reports;

public sealed partial class ReportsService(TecNMDbContext dbContext, ICurrentUser currentUser)
{
    private static readonly string[] AllStatuses = ReportMetricRules.AllStatuses;

    private static readonly string[] AllSectors =
    [
        "educational",
        "public",
        "private",
        "social"
    ];

    private sealed record StatsEvalRow(
        long TramiteId,
        long StudentId,
        DateOnly PeriodStartDate,
        long CareerId,
        string CareerCode,
        string CareerName,
        string? Gender,
        string Status,
        bool Cp,
        bool Ca,
        bool Released
    );

    public async Task<DashboardResponse> GetDashboardAsync(
        long? periodId,
        CancellationToken cancellationToken = default)
    {
        var period = await ResolvePeriodAsync(periodId, cancellationToken);

        var ssCounts = await dbContext.SocialServices
            .AsNoTracking()
            .Where(s => s.PeriodId == period.Id && (currentUser.ReadsAll() || currentUser.Roles.Contains("social_service_officer")))
            .GroupBy(s => s.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);

        var ppCounts = await dbContext.Internships
            .AsNoTracking()
            .Where(i => i.PeriodId == period.Id && (currentUser.ReadsAll() || currentUser.Roles.Contains("internship_coordinator")))
            .GroupBy(i => i.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);

        var ssDict = AllStatuses.ToDictionary(st => st, st => ssCounts.GetValueOrDefault(st, 0));
        var ppDict = AllStatuses.ToDictionary(st => st, st => ppCounts.GetValueOrDefault(st, 0));

        var pendingReviews = await currentUser.VisibleDocuments(dbContext, dbContext.ExpedientDocuments)
            .AsNoTracking()
            .CountAsync(d => d.Status == "pending", cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var in60Days = today.AddDays(60);
        var expiringAgreements = await dbContext.Agreements
            .AsNoTracking()
            .CountAsync(a => a.IsActive && a.ExpirationDate >= today && a.ExpirationDate <= in60Days, cancellationToken);

        var openPncs = await dbContext.NonConformances
            .AsNoTracking()
            .CountAsync(n => n.PeriodId == period.Id && (n.Status == "open" || n.Status == "under_review") && (currentUser.ReadsAll() || n.TramiteType == "social_service" && currentUser.Roles.Contains("social_service_officer") || n.TramiteType == "internship" && currentUser.Roles.Contains("internship_coordinator")), cancellationToken);

        return new DashboardResponse(
            new ReportPeriodSummary(period.Id, period.Code, period.Name),
            ssDict,
            ppDict,
            pendingReviews,
            expiringAgreements,
            openPncs);
    }

    public Task<IReadOnlyList<TrackingRowResponse>> GetTrackingAsync(
        long? periodId,
        string? schoolYear,
        string tramiteType,
        string? status,
        long? careerId,
        int? semester,
        string? gender,
        long? companyId,
        long? activityTypeId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        currentUser.RequireProcess(ValidateTramiteType(tramiteType));
        return GetTrackingCoreAsync(
            periodId, schoolYear, tramiteType, status, careerId, semester, gender, companyId, activityTypeId, search, cancellationToken);
    }

    private async Task<IReadOnlyList<TrackingRowResponse>> GetTrackingCoreAsync(
        long? periodId,
        string? schoolYear,
        string tramiteType,
        string? status,
        long? careerId,
        int? semester,
        string? gender,
        long? companyId,
        long? activityTypeId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var normType = ValidateTramiteType(tramiteType);
        HashSet<long>? targetPeriodIds = null;

        if (!string.IsNullOrWhiteSpace(schoolYear))
        {
            var normSy = schoolYear.Trim();
            var ids = await dbContext.AcademicPeriods
                .AsNoTracking()
                .Where(p => p.SchoolYear == normSy)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);
            if (ids.Count == 0)
            {
                throw new NotFoundException("academic_periods.school_year_not_found", $"Ciclo escolar '{normSy}' no encontrado.");
            }
            targetPeriodIds = ids.ToHashSet();
        }
        else if (periodId.HasValue && periodId.Value > 0)
        {
            var period = await ResolvePeriodAsync(periodId.Value, cancellationToken);
            targetPeriodIds = [period.Id];
        }
        else if (!periodId.HasValue)
        {
            var period = await ResolvePeriodAsync(null, cancellationToken);
            targetPeriodIds = [period.Id];
        }

        if (normType == "social_service")
        {
            var query = dbContext.SocialServices
                .Include(s => s.Student)
                    .ThenInclude(st => st!.Career)
                .Include(s => s.Period)
                .Include(s => s.Company)
                .Include(s => s.Supervisor)
                .Include(s => s.Agreement)
                .Include(s => s.Program)
                .Include(s => s.ActivityType)
                .Include(s => s.Application)
                .AsNoTracking();

            if (targetPeriodIds is not null)
                query = query.Where(s => targetPeriodIds.Contains(s.PeriodId));
            if (!string.IsNullOrWhiteSpace(status))
            {
                var normStatus = status.Trim().ToLowerInvariant();
                query = query.Where(s => s.Status == normStatus);
            }
            if (careerId.HasValue)
                query = query.Where(s => s.Student != null && s.Student.CareerId == careerId.Value);
            if (semester.HasValue)
                query = query.Where(s => s.SemesterEnrolled == semester.Value);
            if (!string.IsNullOrWhiteSpace(gender))
            {
                var g = gender.Trim().ToUpperInvariant();
                query = g == "UNSPECIFIED"
                    ? query.Where(s => s.Student != null && s.Student.Gender == null)
                    : query.Where(s => s.Student != null && s.Student.Gender == g);
            }
            if (companyId.HasValue)
                query = companyId.Value == 0 ? query.Where(_ => false) : query.Where(s => s.CompanyId == companyId.Value);
            if (activityTypeId.HasValue)
                query = activityTypeId.Value == 0 ? query.Where(s => s.ActivityTypeId == null) : query.Where(s => s.ActivityTypeId == activityTypeId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = $"%{search.Trim()}%";
                query = query.Where(s =>
                    s.Student != null && (
                        EF.Functions.ILike(s.Student.ControlNumber, pattern) ||
                        EF.Functions.ILike(s.Student.FirstName, pattern) ||
                        EF.Functions.ILike(s.Student.FirstSurname, pattern) ||
                        (s.Student.SecondSurname != null && EF.Functions.ILike(s.Student.SecondSurname, pattern))));
            }

            var items = await query
                .OrderBy(s => s.Student!.Career!.Code)
                .ThenBy(s => s.Student!.FirstSurname)
                .ThenBy(s => s.Student!.FirstName)
                .ToListAsync(cancellationToken);

            var ids = items.Select(x => x.Id).ToList();
            var docsByTramite = await LoadApprovedCodesByTramiteAsync("social_service", ids, cancellationToken);

            return items.Select(s =>
            {
                docsByTramite.TryGetValue(s.Id, out var approved);
                approved ??= [];

                var cp = DocumentProgressCalculator.DocumentDone(s, approved, "CP");
                var ca = DocumentProgressCalculator.DocumentDone(s, approved, "CA");
                var sol = DocumentProgressCalculator.DocumentDone(s, approved, "SOL");
                var cc = approved.Contains("CC");
                var r1 = approved.Contains("R1");
                var r2 = approved.Contains("R2");
                var r3 = approved.Contains("R3");
                var ev1 = DocumentProgressCalculator.EvaluationDone(approved, s.DocumentWorkflowVersion, 1);
                var ev2 = DocumentProgressCalculator.EvaluationDone(approved, s.DocumentWorkflowVersion, 2);
                var ev3 = DocumentProgressCalculator.EvaluationDone(approved, s.DocumentWorkflowVersion, 3);
                var ct = DocumentProgressCalculator.DocumentDone(s, approved, "CT");

                var progress = DocumentProgressCalculator.SocialService(s, approved);

                var student = s.Student!;
                var fullName = string.Join(" ", new[] { student.FirstSurname, student.SecondSurname, student.FirstName }
                    .Where(x => !string.IsNullOrWhiteSpace(x)));

                return new TrackingRowResponse(
                    s.Id,
                    student.ControlNumber,
                    fullName,
                    student.Career?.Code ?? string.Empty,
                    student.Career?.Name ?? string.Empty,
                    s.SemesterEnrolled,
                    student.Gender,
                    student.Shift,
                    s.Application?.StudentPhone ?? student.PhoneNumber,
                    s.Company?.Name,
                    s.Company?.Sector,
                    s.Supervisor?.FullName,
                    s.Agreement?.AgreementNumber,
                    s.Program?.Name,
                    s.StartDate,
                    s.EndDate,
                    s.Status,
                    progress,
                    new TrackingDocumentsChecklist(cp, ca, sol, cc, r1, r2, r3, ev1, ev2, ev3, null, ct,
                        s.DocumentWorkflowVersion >= 2 ? Enumerable.Range(1, 3).All(n => approved.Contains($"EV_SUP{n}")) : null,
                        s.DocumentWorkflowVersion >= 2 ? Enumerable.Range(1, 3).All(n => approved.Contains($"EV_SELF{n}")) : null,
                        s.DocumentWorkflowVersion >= 2 ? approved.Contains("EV_ACT") : null),
                    s.PresentationLetterFolio,
                    s.ReleaseCertificateFolio,
                    s.OriginalTerminationLetterReceived,
                    s.FinalGradeLevel,
                    s.Status == "non_compliant" || (ca && !ct && s.Status != "released"),
                    s.Observations,
                    s.SchoolServicesActFolio,
                    s.BeneficiariesCount,
                    s.HasInclusionActivities,
                    s.TargetCommunity,
                    s.Period?.Code,
                    s.Period?.SchoolYear,
                    s.ActivityType?.Name);
            }).ToList();
        }
        else
        {
            var query = dbContext.Internships
                .Include(i => i.Student)
                    .ThenInclude(st => st!.Career)
                .Include(i => i.Period)
                .Include(i => i.Company)
                .Include(i => i.Supervisor)
                .Include(i => i.Agreement)
                .Include(i => i.ActivityType)
                .Include(i => i.Evaluation)
                .AsNoTracking();

            if (targetPeriodIds is not null)
                query = query.Where(i => targetPeriodIds.Contains(i.PeriodId));
            if (!string.IsNullOrWhiteSpace(status))
            {
                var normStatus = status.Trim().ToLowerInvariant();
                query = query.Where(i => i.Status == normStatus);
            }
            if (careerId.HasValue)
                query = query.Where(i => i.Student != null && i.Student.CareerId == careerId.Value);
            if (semester.HasValue)
                query = query.Where(i => i.SemesterEnrolled == semester.Value);
            if (!string.IsNullOrWhiteSpace(gender))
            {
                var g = gender.Trim().ToUpperInvariant();
                query = g == "UNSPECIFIED"
                    ? query.Where(i => i.Student != null && i.Student.Gender == null)
                    : query.Where(i => i.Student != null && i.Student.Gender == g);
            }
            if (companyId.HasValue)
                query = companyId.Value == 0 ? query.Where(i => i.CompanyId == null) : query.Where(i => i.CompanyId == companyId.Value);
            if (activityTypeId.HasValue)
                query = activityTypeId.Value == 0 ? query.Where(i => i.ActivityTypeId == null) : query.Where(i => i.ActivityTypeId == activityTypeId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = $"%{search.Trim()}%";
                query = query.Where(i =>
                    i.Student != null && (
                        EF.Functions.ILike(i.Student.ControlNumber, pattern) ||
                        EF.Functions.ILike(i.Student.FirstName, pattern) ||
                        EF.Functions.ILike(i.Student.FirstSurname, pattern) ||
                        (i.Student.SecondSurname != null && EF.Functions.ILike(i.Student.SecondSurname, pattern))));
            }

            var items = await query
                .OrderBy(i => i.Student!.Career!.Code)
                .ThenBy(i => i.Student!.FirstSurname)
                .ThenBy(i => i.Student!.FirstName)
                .ToListAsync(cancellationToken);

            var ids = items.Select(x => x.Id).ToList();
            var docsByTramite = await LoadApprovedCodesByTramiteAsync("internship", ids, cancellationToken);

            return items.Select(i =>
            {
                docsByTramite.TryGetValue(i.Id, out var approved);
                approved ??= [];

                var cp = !string.IsNullOrWhiteSpace(i.PresentationLetterFolio) || i.PresentationLetterDate.HasValue || approved.Contains("CP");
                var ca = i.AcceptanceLetterDate.HasValue || approved.Contains("CA");
                var ev = approved.Contains("EV");
                var ct = i.TerminationLetterDate.HasValue || approved.Contains("CT");
                var cc = i.Status == "released" || i.IsExpedientComplete || !string.IsNullOrWhiteSpace(i.ReleaseCertificateFolio) || approved.Contains("CC");

                var progress = 0;
                if (cp) progress += 20;
                if (ca) progress += 20;
                if (ev) progress += 20;
                if (ct) progress += 20;
                if (cc) progress += 20;

                var student = i.Student!;
                var fullName = string.Join(" ", new[] { student.FirstSurname, student.SecondSurname, student.FirstName }
                    .Where(x => !string.IsNullOrWhiteSpace(x)));

                return new TrackingRowResponse(
                    i.Id,
                    student.ControlNumber,
                    fullName,
                    student.Career?.Code ?? string.Empty,
                    student.Career?.Name ?? string.Empty,
                    i.SemesterEnrolled,
                    student.Gender,
                    student.Shift,
                    student.PhoneNumber,
                    i.Company?.Name,
                    i.Company?.Sector,
                    i.Supervisor?.FullName,
                    i.Agreement?.AgreementNumber,
                    null,
                    i.StartDate,
                    i.EndDate,
                    i.Status,
                    progress,
                    new TrackingDocumentsChecklist(cp, ca, null, cc, null, null, null, null, null, null, ev, ct),
                    i.PresentationLetterFolio,
                    i.ReleaseCertificateFolio,
                    i.OriginalTerminationLetterReceived,
                    i.Evaluation?.PerformanceLevel,
                    i.Status == "non_compliant" || (ca && !cc),
                    i.Observations,
                    i.SchoolServicesActFolio,
                    null,
                    null,
                    null,
                    i.Period?.Code,
                    i.Period?.SchoolYear,
                    i.ActivityType?.Name);
            }).ToList();
        }
    }

    public async Task<StatisticalReportResponse> GetStatisticsAsync(
        long? periodId,
        long? comparePeriodId,
        string? schoolYear,
        string? compareSchoolYear,
        string tramiteType,
        CancellationToken cancellationToken = default)
    {
        var normType = ValidateTramiteType(tramiteType);

        await using var tx = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.RepeatableRead, cancellationToken);

        var allPeriods = await dbContext.AcademicPeriods
            .AsNoTracking()
            .OrderBy(p => p.StartDate)
            .ToListAsync(cancellationToken);

        if (allPeriods.Count == 0)
        {
            throw new NotFoundException("academic_periods.not_found", "No existen periodos académicos registrados.");
        }

        List<AcademicPeriod> targetPeriods;
        ReportPeriodSummary periodSummary;
        if (!string.IsNullOrWhiteSpace(schoolYear))
        {
            var sy = schoolYear.Trim();
            targetPeriods = allPeriods.Where(p => p.SchoolYear == sy).ToList();
            if (targetPeriods.Count == 0)
                throw new NotFoundException("academic_periods.school_year_not_found", $"Ciclo escolar '{sy}' no encontrado.");
            periodSummary = new ReportPeriodSummary(0, sy, $"Ciclo Escolar {sy}");
        }
        else
        {
            var p = periodId.HasValue
                ? allPeriods.SingleOrDefault(x => x.Id == periodId.Value)
                    ?? throw new NotFoundException("academic_periods.not_found", $"Periodo académico #{periodId} no encontrado.")
                : allPeriods.OrderByDescending(x => x.IsActive).ThenByDescending(x => x.StartDate).First();
            targetPeriods = [p];
            periodSummary = new ReportPeriodSummary(p.Id, p.Code, p.Name);
        }

        var rows = await LoadStatisticsRowsAsync(normType, targetPeriods.Select(p => p.Id).ToHashSet(), cancellationToken);

        var activeCareers = await dbContext.Careers
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Id)
            .Select(c => new { c.Id, c.Code, c.Name })
            .ToListAsync(cancellationToken);

        // Incluir carreras inactivas que tengan trámites en el periodo/ciclo para no perder registros
        var careerMap = activeCareers.ToDictionary(c => c.Id, c => (c.Code, c.Name));
        foreach (var r in rows)
        {
            if (!careerMap.ContainsKey(r.CareerId))
            {
                careerMap[r.CareerId] = (r.CareerCode, r.CareerName);
            }
        }

        var byCareer = careerMap.Select(kvp =>
        {
            var cRows = rows.Where(r => r.CareerId == kvp.Key).ToList();
            var eligible = cRows.Count(r => r.Cp);
            var appMale = cRows.Count(r => r.Ca && r.Gender == "M");
            var appFemale = cRows.Count(r => r.Ca && r.Gender == "F");
            var appTotal = cRows.Count(r => r.Ca);
            var relMale = cRows.Count(r => r.Released && r.Gender == "M");
            var relFemale = cRows.Count(r => r.Released && r.Gender == "F");
            var relTotal = cRows.Count(r => r.Released);
            var pncTotal = Math.Max(0, appTotal - relTotal);
            var rate = appTotal > 0 ? Math.Round(relTotal * 100m / appTotal, 2) : 0m;

            return new CareerStatisticalBreakdown(
                kvp.Value.Code,
                kvp.Value.Name,
                eligible,
                appMale,
                appFemale,
                appTotal,
                relMale,
                relFemale,
                relTotal,
                pncTotal,
                rate);
        }).ToList();

        var totEligible = byCareer.Sum(x => x.EligibleWithPresentationLetter);
        var totAppMale = byCareer.Sum(x => x.ApplicantsMale);
        var totAppFemale = byCareer.Sum(x => x.ApplicantsFemale);
        var totApp = byCareer.Sum(x => x.ApplicantsTotal);
        var totRelMale = byCareer.Sum(x => x.ReleasedMale);
        var totRelFemale = byCareer.Sum(x => x.ReleasedFemale);
        var totRel = byCareer.Sum(x => x.ReleasedTotal);
        var totPnc = Math.Max(0, totApp - totRel);
        var totRate = totApp > 0 ? Math.Round(totRel * 100m / totApp, 2) : 0m;

        var totals = new StatisticalTotals(
            totEligible, totAppMale, totAppFemale, totApp,
            totRelMale, totRelFemale, totRel, totPnc, totRate);

        // Consolidado comparativo deduplicado por estudiante (sin doble conteo entre periodos)
        SchoolYearComparison? comparison = null;
        if (!string.IsNullOrWhiteSpace(compareSchoolYear))
        {
            var cmpSy = compareSchoolYear.Trim();
            var cmpIds = allPeriods.Where(p => p.SchoolYear == cmpSy).Select(p => p.Id).ToHashSet();
            if (cmpIds.Count > 0)
            {
                var combinedIds = targetPeriods.Select(p => p.Id).Concat(cmpIds).ToHashSet();
                var combined = await LoadStatisticsRowsAsync(normType, combinedIds, cancellationToken);
                var cElig = combined.Count(r => r.Cp);
                var cApp = combined.Count(r => r.Ca);
                var cRel = combined.Count(r => r.Released);
                var cPnc = Math.Max(0, cApp - cRel);
                var cRate = cApp > 0 ? Math.Round(cRel * 100m / cApp, 2) : 0m;
                comparison = new SchoolYearComparison(cmpSy, periodSummary.Code, cElig, cApp, cRel, cPnc, cRate);
            }
        }
        else if (comparePeriodId.HasValue)
        {
            var prevPeriod = allPeriods.SingleOrDefault(p => p.Id == comparePeriodId.Value);
            if (prevPeriod is not null)
            {
                var combinedIds = targetPeriods.Select(p => p.Id).Append(prevPeriod.Id).ToHashSet();
                var combined = await LoadStatisticsRowsAsync(normType, combinedIds, cancellationToken);
                var cElig = combined.Count(r => r.Cp);
                var cApp = combined.Count(r => r.Ca);
                var cRel = combined.Count(r => r.Released);
                var cPnc = Math.Max(0, cApp - cRel);
                var cRate = cApp > 0 ? Math.Round(cRel * 100m / cApp, 2) : 0m;
                comparison = new SchoolYearComparison(prevPeriod.Code, periodSummary.Code, cElig, cApp, cRel, cPnc, cRate);
            }
        }

        await tx.CommitAsync(cancellationToken);

        return new StatisticalReportResponse(periodSummary, normType, byCareer, totals, comparison);
    }

    public async Task<Report911Response> GetReport911Async(
        long? periodId,
        long? comparePeriodId,
        string? schoolYear,
        string tramiteType,
        CancellationToken cancellationToken = default)
    {
        var normType = ValidateTramiteType(tramiteType);

        await using var tx = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.RepeatableRead, cancellationToken);

        var allPeriods = await dbContext.AcademicPeriods
            .AsNoTracking()
            .OrderBy(p => p.StartDate)
            .ToListAsync(cancellationToken);

        if (allPeriods.Count == 0)
        {
            throw new NotFoundException("academic_periods.not_found", "No existen periodos académicos registrados.");
        }

        HashSet<long> periodIds;
        ReportPeriodSummary periodSummary;
        if (!string.IsNullOrWhiteSpace(schoolYear))
        {
            var sy = schoolYear.Trim();
            var matched = allPeriods.Where(p => p.SchoolYear == sy).ToList();
            if (matched.Count == 0)
                throw new NotFoundException("academic_periods.school_year_not_found", $"Ciclo escolar '{sy}' no encontrado.");
            periodIds = matched.Select(p => p.Id).ToHashSet();
            periodSummary = new ReportPeriodSummary(0, sy, $"Ciclo Escolar {sy}");
        }
        else
        {
            var period = periodId.HasValue
                ? allPeriods.SingleOrDefault(p => p.Id == periodId.Value)
                    ?? throw new NotFoundException("academic_periods.not_found", $"Periodo académico #{periodId} no encontrado.")
                : allPeriods.OrderByDescending(p => p.IsActive).ThenByDescending(p => p.StartDate).First();
            periodIds = [period.Id];
            if (comparePeriodId.HasValue && comparePeriodId.Value != period.Id)
            {
                periodIds.Add(comparePeriodId.Value);
            }
            periodSummary = new ReportPeriodSummary(period.Id, period.Code, period.Name);
        }

        var careers = await dbContext.Careers
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Id)
            .Select(c => new { c.Id, c.Code })
            .ToListAsync(cancellationToken);

        if (normType == "social_service")
        {
            var raw = await dbContext.SocialServices
                .AsNoTracking()
                .Where(s => periodIds.Contains(s.PeriodId) && s.Student != null && s.Period != null)
                .Select(s => new
                {
                    s.Id,
                    s.StudentId,
                    s.Status,
                    s.DocumentWorkflowVersion,
                    s.AcceptanceLetterDate,
                    s.TerminationLetterDate,
                    s.ReleaseCertificateFolio,
                    s.HasInclusionActivities,
                    s.BeneficiariesCount,
                    PeriodStartDate = s.Period!.StartDate,
                    CareerId = s.Student!.CareerId,
                    Gender = s.Student.Gender,
                    HasDisability = s.Student.HasDisability,
                    Shift = s.Student.Shift,
                    CompanySector = s.Company != null ? s.Company.Sector : null,
                    CompanySize = s.Company != null ? s.Company.CompanySize : null
                })
                .ToListAsync(cancellationToken);

            var docsByTramite = await LoadApprovedCodesByTramiteAsync("social_service", raw.Select(r => r.Id).ToList(), cancellationToken);

            var dedup = raw
                .GroupBy(r => r.StudentId)
                .Select(g => g
                    .OrderByDescending(r => ReportMetricRules.StatusPriority(r.Status))
                    .ThenByDescending(r => r.PeriodStartDate)
                    .ThenByDescending(r => r.Id)
                    .First())
                .ToList();

            var releasedList = dedup.Where(r =>
            {
                docsByTramite.TryGetValue(r.Id, out var approved);
                var ctApproved = approved?.Contains("CT") == true;
                return ReportMetricRules.IsSocialServiceReleased(r.Status, ctApproved, r.ReleaseCertificateFolio, r.TerminationLetterDate);
            }).ToList();

            var placedList = dedup.Where(r =>
            {
                docsByTramite.TryGetValue(r.Id, out var approved);
                var caApproved = approved?.Contains("CA") == true || (r.DocumentWorkflowVersion < 2 && r.AcceptanceLetterDate.HasValue);
                return caApproved || r.Status is "accepted" or "active" or "completed_pending_review" or "released";
            }).ToList();

            var byCareerAndGender = new List<Report911CareerGenderRow>();
            foreach (var c in careers)
            {
                foreach (var sector in AllSectors)
                {
                    var group = releasedList.Where(s => s.CareerId == c.Id && s.CompanySector == sector).ToList();
                    var m = group.Count(s => s.Gender == "M");
                    var f = group.Count(s => s.Gender == "F");
                    var u = group.Count - m - f;
                    var dis = group.Count(s => s.HasDisability);
                    byCareerAndGender.Add(new Report911CareerGenderRow(c.Code, sector, m, f, u, group.Count, dis));
                }
            }

            var bySector = AllSectors.Select(sector =>
            {
                var group = releasedList.Where(s => s.CompanySector == sector).ToList();
                return new Report911SectorRow(
                    sector,
                    group.Count(s => s.Gender == "M"),
                    group.Count(s => s.Gender == "F"),
                    group.Count,
                    group.Count(s => s.HasDisability));
            }).ToList();

            // Sección XIII: estudiantes colocados (CA = SI) por tamaño de empresa
            var bySize = placedList
                .Where(s => !string.IsNullOrWhiteSpace(s.CompanySize))
                .GroupBy(s => s.CompanySize!)
                .Select(g => new Report911CompanySizeRow(g.Key, g.Count()))
                .ToList();

            var disMale = releasedList.Count(s => s.HasDisability && s.Gender == "M");
            var disFemale = releasedList.Count(s => s.HasDisability && s.Gender == "F");
            var disTotal = releasedList.Count(s => s.HasDisability);

            var byShift = releasedList
                .GroupBy(s => s.Shift ?? "morning")
                .Select(g => new Report911ShiftRow(g.Key, g.Count()))
                .ToList();

            await tx.CommitAsync(cancellationToken);

            return new Report911Response(
                periodSummary,
                normType,
                byCareerAndGender,
                bySector,
                bySize,
                new Report911DisabilitySummary(disMale, disFemale, disTotal),
                byShift,
                releasedList.Count(s => s.HasInclusionActivities),
                releasedList.Sum(s => s.BeneficiariesCount));
        }
        else
        {
            var raw = await dbContext.Internships
                .AsNoTracking()
                .Where(i => periodIds.Contains(i.PeriodId) && i.Student != null && i.Period != null)
                .Select(i => new
                {
                    i.Id,
                    i.StudentId,
                    i.Status,
                    i.AcceptanceLetterDate,
                    i.IsExpedientComplete,
                    i.ReleaseCertificateFolio,
                    PeriodStartDate = i.Period!.StartDate,
                    CareerId = i.Student!.CareerId,
                    Gender = i.Student.Gender,
                    HasDisability = i.Student.HasDisability,
                    Shift = i.Student.Shift,
                    CompanySector = i.Company != null ? i.Company.Sector : null,
                    CompanySize = i.Company != null ? i.Company.CompanySize : null
                })
                .ToListAsync(cancellationToken);

            var docsByTramite = await LoadApprovedCodesByTramiteAsync("internship", raw.Select(r => r.Id).ToList(), cancellationToken);

            var dedup = raw
                .GroupBy(r => r.StudentId)
                .Select(g => g
                    .OrderByDescending(r => ReportMetricRules.StatusPriority(r.Status))
                    .ThenByDescending(r => r.PeriodStartDate)
                    .ThenByDescending(r => r.Id)
                    .First())
                .ToList();

            var releasedList = dedup.Where(r =>
            {
                docsByTramite.TryGetValue(r.Id, out var approved);
                var ca = r.AcceptanceLetterDate.HasValue || approved?.Contains("CA") == true;
                var cc = approved?.Contains("CC") == true;
                return ReportMetricRules.IsInternshipReleased(r.Status, ca, cc, r.IsExpedientComplete, r.ReleaseCertificateFolio);
            }).ToList();

            var placedList = dedup.Where(r =>
            {
                docsByTramite.TryGetValue(r.Id, out var approved);
                return r.AcceptanceLetterDate.HasValue || approved?.Contains("CA") == true || r.Status is "accepted" or "active" or "completed_pending_review" or "released";
            }).ToList();

            var byCareerAndGender = new List<Report911CareerGenderRow>();
            foreach (var c in careers)
            {
                foreach (var sector in AllSectors)
                {
                    var group = releasedList.Where(i => i.CareerId == c.Id && i.CompanySector == sector).ToList();
                    var m = group.Count(i => i.Gender == "M");
                    var f = group.Count(i => i.Gender == "F");
                    var u = group.Count - m - f;
                    var dis = group.Count(i => i.HasDisability);
                    byCareerAndGender.Add(new Report911CareerGenderRow(c.Code, sector, m, f, u, group.Count, dis));
                }
            }

            var bySector = AllSectors.Select(sector =>
            {
                var group = releasedList.Where(i => i.CompanySector == sector).ToList();
                return new Report911SectorRow(
                    sector,
                    group.Count(i => i.Gender == "M"),
                    group.Count(i => i.Gender == "F"),
                    group.Count,
                    group.Count(i => i.HasDisability));
            }).ToList();

            var bySize = placedList
                .Where(i => !string.IsNullOrWhiteSpace(i.CompanySize))
                .GroupBy(i => i.CompanySize!)
                .Select(g => new Report911CompanySizeRow(g.Key, g.Count()))
                .ToList();

            var disMale = releasedList.Count(i => i.HasDisability && i.Gender == "M");
            var disFemale = releasedList.Count(i => i.HasDisability && i.Gender == "F");
            var disTotal = releasedList.Count(i => i.HasDisability);

            var byShift = releasedList
                .GroupBy(i => i.Shift ?? "morning")
                .Select(g => new Report911ShiftRow(g.Key, g.Count()))
                .ToList();

            await tx.CommitAsync(cancellationToken);

            return new Report911Response(
                periodSummary,
                normType,
                byCareerAndGender,
                bySector,
                bySize,
                new Report911DisabilitySummary(disMale, disFemale, disTotal),
                byShift,
                0,
                0);
        }
    }

    public async Task<NonConformanceIndicatorResponse> GetNonConformanceIndicatorAsync(
        long? periodId,
        string tramiteType,
        CancellationToken cancellationToken = default)
    {
        var normType = ValidateTramiteType(tramiteType);
        var stats = await GetStatisticsAsync(periodId, null, null, null, normType, cancellationToken);

        var pncs = await dbContext.NonConformances
            .AsNoTracking()
            .Where(n => n.PeriodId == stats.Period.Id && n.TramiteType == normType)
            .ToListAsync(cancellationToken);

        var openPncs = pncs.Count(n => n.Status is "open" or "under_review");
        var byCategory = pncs
            .GroupBy(n => n.Category)
            .Select(g => new NonConformanceCategoryCount(g.Key, g.Count()))
            .ToList();

        var rate = stats.Totals.ApplicantsTotal > 0
            ? Math.Round((decimal)stats.Totals.ReleasedTotal / stats.Totals.ApplicantsTotal, 4)
            : 0m;

        return new NonConformanceIndicatorResponse(
            stats.Period,
            normType,
            stats.Totals.ApplicantsTotal,
            stats.Totals.ReleasedTotal,
            stats.Totals.PncTotal,
            openPncs,
            rate,
            byCategory);
    }

    public async Task<CycleTimesResponse> GetCycleTimesAsync(
        string tramiteType,
        CancellationToken cancellationToken = default)
    {
        var normType = ValidateTramiteType(tramiteType);

        var logs = await dbContext.TramiteStatusLogs
            .AsNoTracking()
            .Where(l => l.TramiteType == normType)
            .OrderBy(l => l.TramiteId)
            .ThenBy(l => l.CreatedAt)
            .ToListAsync(cancellationToken);

        var transitions = new List<(string From, string To, double Days)>();
        foreach (var group in logs.GroupBy(l => l.TramiteId))
        {
            var ordered = group.ToList();
            for (var idx = 1; idx < ordered.Count; idx++)
            {
                var prev = ordered[idx - 1];
                var curr = ordered[idx];
                var days = Math.Max(0, (curr.CreatedAt - prev.CreatedAt).TotalDays);
                transitions.Add((prev.NewStatus, curr.NewStatus, days));
            }
        }

        var summary = transitions
            .GroupBy(t => (t.From, t.To))
            .Select(g => new CycleTimeTransitionRow(
                g.Key.From,
                g.Key.To,
                Math.Round((decimal)g.Average(x => x.Days), 2),
                g.Count()))
            .ToList();

        return new CycleTimesResponse(normType, summary);
    }

    private async Task<List<StatsEvalRow>> LoadStatisticsRowsAsync(
        string normType,
        HashSet<long> periodIds,
        CancellationToken cancellationToken)
    {
        if (normType == "social_service")
        {
            var raw = await dbContext.SocialServices
                .AsNoTracking()
                .Where(s => periodIds.Contains(s.PeriodId) && s.Student != null && s.Period != null && s.Student.Career != null)
                .Select(s => new
                {
                    s.Id,
                    s.StudentId,
                    s.Status,
                    s.DocumentWorkflowVersion,
                    s.PresentationLetterFolio,
                    s.PresentationLetterDate,
                    s.AcceptanceLetterDate,
                    s.TerminationLetterDate,
                    s.ReleaseCertificateFolio,
                    PeriodStartDate = s.Period!.StartDate,
                    CareerId = s.Student!.CareerId,
                    CareerCode = s.Student.Career!.Code,
                    CareerName = s.Student.Career.Name,
                    Gender = s.Student.Gender
                })
                .ToListAsync(cancellationToken);

            var docsByTramite = await LoadApprovedCodesByTramiteAsync("social_service", raw.Select(r => r.Id).ToList(), cancellationToken);

            var evaluated = raw.Select(s =>
            {
                docsByTramite.TryGetValue(s.Id, out var approved);
                approved ??= [];
                var cp = approved.Contains("CP") || (s.DocumentWorkflowVersion < 2 && (!string.IsNullOrWhiteSpace(s.PresentationLetterFolio) || s.PresentationLetterDate.HasValue));
                var ca = approved.Contains("CA") || (s.DocumentWorkflowVersion < 2 && s.AcceptanceLetterDate.HasValue);
                var ct = approved.Contains("CT") || (s.DocumentWorkflowVersion < 2 && s.TerminationLetterDate.HasValue);
                var released = ReportMetricRules.IsSocialServiceReleased(s.Status, ct, s.ReleaseCertificateFolio, s.TerminationLetterDate);
                return new StatsEvalRow(s.Id, s.StudentId, s.PeriodStartDate, s.CareerId, s.CareerCode, s.CareerName, s.Gender, s.Status, cp, ca, released);
            });

            return evaluated
                .GroupBy(r => r.StudentId)
                .Select(g => g
                    .OrderByDescending(r => r.Released ? 1 : 0)
                    .ThenByDescending(r => ReportMetricRules.StatusPriority(r.Status))
                    .ThenByDescending(r => r.PeriodStartDate)
                    .ThenByDescending(r => r.TramiteId)
                    .First())
                .ToList();
        }
        else
        {
            var raw = await dbContext.Internships
                .AsNoTracking()
                .Where(i => periodIds.Contains(i.PeriodId) && i.Student != null && i.Period != null && i.Student.Career != null)
                .Select(i => new
                {
                    i.Id,
                    i.StudentId,
                    i.Status,
                    i.PresentationLetterFolio,
                    i.PresentationLetterDate,
                    i.AcceptanceLetterDate,
                    i.IsExpedientComplete,
                    i.ReleaseCertificateFolio,
                    PeriodStartDate = i.Period!.StartDate,
                    CareerId = i.Student!.CareerId,
                    CareerCode = i.Student.Career!.Code,
                    CareerName = i.Student.Career.Name,
                    Gender = i.Student.Gender
                })
                .ToListAsync(cancellationToken);

            var docsByTramite = await LoadApprovedCodesByTramiteAsync("internship", raw.Select(r => r.Id).ToList(), cancellationToken);

            var evaluated = raw.Select(i =>
            {
                docsByTramite.TryGetValue(i.Id, out var approved);
                approved ??= [];
                var cp = !string.IsNullOrWhiteSpace(i.PresentationLetterFolio) || i.PresentationLetterDate.HasValue || approved.Contains("CP");
                var ca = i.AcceptanceLetterDate.HasValue || approved.Contains("CA");
                var cc = approved.Contains("CC");
                var released = ReportMetricRules.IsInternshipReleased(i.Status, ca, cc, i.IsExpedientComplete, i.ReleaseCertificateFolio);
                return new StatsEvalRow(i.Id, i.StudentId, i.PeriodStartDate, i.CareerId, i.CareerCode, i.CareerName, i.Gender, i.Status, cp, ca, released);
            });

            return evaluated
                .GroupBy(r => r.StudentId)
                .Select(g => g
                    .OrderByDescending(r => r.Released ? 1 : 0)
                    .ThenByDescending(r => ReportMetricRules.StatusPriority(r.Status))
                    .ThenByDescending(r => r.PeriodStartDate)
                    .ThenByDescending(r => r.TramiteId)
                    .First())
                .ToList();
        }
    }

    private async Task<AcademicPeriod> ResolvePeriodAsync(long? periodId, CancellationToken cancellationToken)
    {
        if (periodId.HasValue)
        {
            return await dbContext.AcademicPeriods
                .AsNoTracking()
                .SingleOrDefaultAsync(p => p.Id == periodId.Value, cancellationToken)
                ?? throw new NotFoundException("academic_periods.not_found", $"Periodo académico #{periodId} no encontrado.");
        }

        return await dbContext.AcademicPeriods
            .AsNoTracking()
            .OrderByDescending(p => p.IsActive)
            .ThenByDescending(p => p.StartDate)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("academic_periods.not_found", "No existen periodos académicos registrados.");
    }

    private async Task<Dictionary<long, HashSet<string>>> LoadApprovedCodesByTramiteAsync(
        string tramiteType,
        IReadOnlyCollection<long> tramiteIds,
        CancellationToken cancellationToken)
    {
        if (tramiteIds.Count == 0)
        {
            return new Dictionary<long, HashSet<string>>();
        }

        var docs = await dbContext.ExpedientDocuments
            .AsNoTracking()
            .Where(d => d.TramiteType == tramiteType
                        && d.TramiteId.HasValue
                        && tramiteIds.Contains(d.TramiteId.Value)
                        && d.Status == "approved")
            .Select(d => new { TramiteId = d.TramiteId!.Value, d.DocumentCode })
            .ToListAsync(cancellationToken);

        return docs
            .GroupBy(d => d.TramiteId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.DocumentCode).ToHashSet(StringComparer.OrdinalIgnoreCase));
    }

    private static string ValidateTramiteType(string? tramiteType)
    {
        var norm = tramiteType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (norm is not ("social_service" or "internship"))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["tramiteType"] = ["El parámetro tramiteType debe ser 'social_service' o 'internship'."]
            });
        }

        return norm;
    }
}
