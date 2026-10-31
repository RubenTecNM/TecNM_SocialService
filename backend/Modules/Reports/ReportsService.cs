using Microsoft.EntityFrameworkCore;
using TecNM.Api.Core.Entities;
using TecNM.Api.Core.Exceptions;
using TecNM.Api.Data;

namespace TecNM.Api.Modules.Reports;

public sealed class ReportsService(TecNMDbContext dbContext)
{
    private static readonly string[] AllStatuses =
    [
        "requested",
        "approved",
        "accepted",
        "active",
        "completed_pending_review",
        "released",
        "cancelled",
        "non_compliant"
    ];

    private static readonly string[] AllSectors =
    [
        "educational",
        "public",
        "private",
        "social"
    ];

    public async Task<DashboardResponse> GetDashboardAsync(
        long? periodId,
        CancellationToken cancellationToken = default)
    {
        var period = await ResolvePeriodAsync(periodId, cancellationToken);

        var ssCounts = await dbContext.SocialServices
            .AsNoTracking()
            .Where(s => s.PeriodId == period.Id)
            .GroupBy(s => s.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);

        var ppCounts = await dbContext.Internships
            .AsNoTracking()
            .Where(i => i.PeriodId == period.Id)
            .GroupBy(i => i.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);

        var ssDict = AllStatuses.ToDictionary(st => st, st => ssCounts.GetValueOrDefault(st, 0));
        var ppDict = AllStatuses.ToDictionary(st => st, st => ppCounts.GetValueOrDefault(st, 0));

        var pendingReviews = await dbContext.ExpedientDocuments
            .AsNoTracking()
            .CountAsync(d => d.Status == "pending", cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var in60Days = today.AddDays(60);
        var expiringAgreements = await dbContext.Agreements
            .AsNoTracking()
            .CountAsync(a => a.IsActive && a.ExpirationDate >= today && a.ExpirationDate <= in60Days, cancellationToken);

        var openPncs = await dbContext.NonConformances
            .AsNoTracking()
            .CountAsync(n => n.PeriodId == period.Id && (n.Status == "open" || n.Status == "under_review"), cancellationToken);

        return new DashboardResponse(
            new ReportPeriodSummary(period.Id, period.Code, period.Name),
            ssDict,
            ppDict,
            pendingReviews,
            expiringAgreements,
            openPncs);
    }

    public async Task<IReadOnlyList<TrackingRowResponse>> GetTrackingAsync(
        long? periodId,
        string tramiteType,
        string? status,
        long? careerId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var normType = ValidateTramiteType(tramiteType);
        var period = await ResolvePeriodAsync(periodId, cancellationToken);

        if (normType == "social_service")
        {
            var query = dbContext.SocialServices
                .Include(s => s.Student)
                    .ThenInclude(st => st!.Career)
                .Include(s => s.Company)
                .Include(s => s.Supervisor)
                .Include(s => s.Agreement)
                .Include(s => s.Program)
                .Include(s => s.Application)
                .AsNoTracking()
                .Where(s => s.PeriodId == period.Id);

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normStatus = status.Trim().ToLowerInvariant();
                query = query.Where(s => s.Status == normStatus);
            }

            if (careerId.HasValue)
            {
                query = query.Where(s => s.Student != null && s.Student.CareerId == careerId.Value);
            }

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

                var cp = !string.IsNullOrWhiteSpace(s.PresentationLetterFolio) || s.PresentationLetterDate.HasValue || approved.Contains("CP");
                var ca = s.AcceptanceLetterDate.HasValue || approved.Contains("CA");
                var sol = s.Application?.IsAcceptedByOffice == true || approved.Contains("SOL");
                var cc = approved.Contains("CC");
                var r1 = approved.Contains("R1");
                var r2 = approved.Contains("R2");
                var r3 = approved.Contains("R3");
                var ev1 = approved.Contains("EV1");
                var ev2 = approved.Contains("EV2");
                var ev3 = approved.Contains("EV3");
                var ct = s.TerminationLetterDate.HasValue || approved.Contains("CT");
                var cot = s.Status == "released" || !string.IsNullOrWhiteSpace(s.ReleaseCertificateFolio) || approved.Contains("COT");

                var progress = 0;
                if (cp) progress += 10;
                if (ca) progress += 10;
                if (sol) progress += 10;
                if (cc) progress += 10;
                if (r1) progress += 10;
                if (r2) progress += 10;
                if (r3) progress += 10;
                if (ev1 || ev2 || ev3) progress += 10;
                if (ct) progress += 10;
                if (cot) progress += 10;

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
                    new TrackingDocumentsChecklist(cp, ca, sol, cc, r1, r2, r3, ev1, ev2, ev3, null, ct),
                    s.PresentationLetterFolio,
                    s.ReleaseCertificateFolio,
                    s.OriginalTerminationLetterReceived,
                    s.FinalGradeLevel,
                    s.Status == "non_compliant" || (ca && !ct && s.Status != "released"),
                    s.Observations,
                    s.SchoolServicesActFolio,
                    s.BeneficiariesCount,
                    s.HasInclusionActivities,
                    s.TargetCommunity);
            }).ToList();
        }
        else
        {
            var query = dbContext.Internships
                .Include(i => i.Student)
                    .ThenInclude(st => st!.Career)
                .Include(i => i.Company)
                .Include(i => i.Supervisor)
                .Include(i => i.Agreement)
                .Include(i => i.Evaluation)
                .AsNoTracking()
                .Where(i => i.PeriodId == period.Id);

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normStatus = status.Trim().ToLowerInvariant();
                query = query.Where(i => i.Status == normStatus);
            }

            if (careerId.HasValue)
            {
                query = query.Where(i => i.Student != null && i.Student.CareerId == careerId.Value);
            }

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
                    null);
            }).ToList();
        }
    }

    public async Task<StatisticalReportResponse> GetStatisticsAsync(
        long? periodId,
        long? comparePeriodId,
        string tramiteType,
        CancellationToken cancellationToken = default)
    {
        var normType = ValidateTramiteType(tramiteType);
        var period = await ResolvePeriodAsync(periodId, cancellationToken);
        var careers = await dbContext.Careers
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Id)
            .ToListAsync(cancellationToken);

        var rows = await GetTrackingAsync(period.Id, normType, null, null, null, cancellationToken);

        var byCareer = careers.Select(c =>
        {
            var cRows = rows.Where(r => string.Equals(r.CareerCode, c.Code, StringComparison.OrdinalIgnoreCase)).ToList();

            var eligible = cRows.Count(r => r.Documents.Cp);
            var appMale = cRows.Count(r => r.Documents.Ca && r.Gender == "M");
            var appFemale = cRows.Count(r => r.Documents.Ca && r.Gender == "F");
            var appTotal = cRows.Count(r => r.Documents.Ca);

            bool IsReleasedRow(TrackingRowResponse r) =>
                normType == "social_service"
                    ? (r.Documents.Ct || r.Status == "released" || !string.IsNullOrWhiteSpace(r.ReleaseCertificateFolio))
                    : (r.Documents.Ca && (r.Documents.Cc == true || r.Status == "released"));

            var relMale = cRows.Count(r => IsReleasedRow(r) && r.Gender == "M");
            var relFemale = cRows.Count(r => IsReleasedRow(r) && r.Gender == "F");
            var relTotal = cRows.Count(IsReleasedRow);
            var pncTotal = Math.Max(0, appTotal - relTotal);
            var rate = appTotal > 0 ? Math.Round(relTotal * 100m / appTotal, 2) : 0m;

            return new CareerStatisticalBreakdown(
                c.Code,
                c.Name,
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
            totEligible,
            totAppMale,
            totAppFemale,
            totApp,
            totRelMale,
            totRelFemale,
            totRel,
            totPnc,
            totRate);

        SchoolYearComparison? comparison = null;
        if (comparePeriodId.HasValue)
        {
            var prevPeriod = await dbContext.AcademicPeriods
                .AsNoTracking()
                .SingleOrDefaultAsync(p => p.Id == comparePeriodId.Value, cancellationToken);

            if (prevPeriod is not null)
            {
                var prevRows = await GetTrackingAsync(prevPeriod.Id, normType, null, null, null, cancellationToken);
                var prevEligible = prevRows.Count(r => r.Documents.Cp);
                var prevApp = prevRows.Count(r => r.Documents.Ca);
                var prevRel = prevRows.Count(r =>
                    normType == "social_service"
                        ? (r.Documents.Ct || r.Status == "released" || !string.IsNullOrWhiteSpace(r.ReleaseCertificateFolio))
                        : (r.Documents.Ca && (r.Documents.Cc == true || r.Status == "released")));

                var combinedEligible = prevEligible + totEligible;
                var combinedApp = prevApp + totApp;
                var combinedRel = prevRel + totRel;
                var combinedPnc = Math.Max(0, combinedApp - combinedRel);
                var combinedRate = combinedApp > 0 ? Math.Round(combinedRel * 100m / combinedApp, 2) : 0m;

                comparison = new SchoolYearComparison(
                    prevPeriod.Code,
                    period.Code,
                    combinedEligible,
                    combinedApp,
                    combinedRel,
                    combinedPnc,
                    combinedRate);
            }
        }

        return new StatisticalReportResponse(
            new ReportPeriodSummary(period.Id, period.Code, period.Name),
            normType,
            byCareer,
            totals,
            comparison);
    }

    public async Task<Report911Response> GetReport911Async(
        long? periodId,
        long? comparePeriodId,
        string tramiteType,
        CancellationToken cancellationToken = default)
    {
        var normType = ValidateTramiteType(tramiteType);
        var period = await ResolvePeriodAsync(periodId, cancellationToken);
        var periodIds = new List<long> { period.Id };
        if (comparePeriodId.HasValue && comparePeriodId.Value != period.Id)
        {
            periodIds.Add(comparePeriodId.Value);
        }

        var careers = await dbContext.Careers
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Id)
            .ToListAsync(cancellationToken);

        if (normType == "social_service")
        {
            var releasedList = await dbContext.SocialServices
                .Include(s => s.Student)
                    .ThenInclude(st => st!.Career)
                .Include(s => s.Company)
                .AsNoTracking()
                .Where(s => periodIds.Contains(s.PeriodId) && (s.Status == "released" || s.TerminationLetterDate != null))
                .ToListAsync(cancellationToken);

            var byCareerAndGender = new List<Report911CareerGenderRow>();
            foreach (var c in careers)
            {
                foreach (var sector in AllSectors)
                {
                    var group = releasedList
                        .Where(s => s.Student?.CareerId == c.Id && (s.Company?.Sector ?? "public") == sector)
                        .ToList();

                    var m = group.Count(s => s.Student?.Gender == "M");
                    var f = group.Count(s => s.Student?.Gender == "F");
                    var u = group.Count - m - f;
                    var dis = group.Count(s => s.Student?.HasDisability == true);

                    byCareerAndGender.Add(new Report911CareerGenderRow(c.Code, sector, m, f, u, group.Count, dis));
                }
            }

            var bySector = AllSectors.Select(sector =>
            {
                var group = releasedList.Where(s => (s.Company?.Sector ?? "public") == sector).ToList();
                var m = group.Count(s => s.Student?.Gender == "M");
                var f = group.Count(s => s.Student?.Gender == "F");
                var dis = group.Count(s => s.Student?.HasDisability == true);
                return new Report911SectorRow(sector, m, f, group.Count, dis);
            }).ToList();

            var bySize = releasedList
                .GroupBy(s => s.Company?.CompanySize ?? "not_applicable")
                .Select(g => new Report911CompanySizeRow(g.Key, g.Count()))
                .ToList();

            var disMale = releasedList.Count(s => s.Student?.HasDisability == true && s.Student?.Gender == "M");
            var disFemale = releasedList.Count(s => s.Student?.HasDisability == true && s.Student?.Gender == "F");

            var byShift = releasedList
                .GroupBy(s => s.Student?.Shift ?? "morning")
                .Select(g => new Report911ShiftRow(g.Key, g.Count()))
                .ToList();

            var withInclusion = releasedList.Count(s => s.HasInclusionActivities);
            var totalBeneficiaries = releasedList.Sum(s => s.BeneficiariesCount);

            return new Report911Response(
                new ReportPeriodSummary(period.Id, period.Code, period.Name),
                normType,
                byCareerAndGender,
                bySector,
                bySize,
                new Report911DisabilitySummary(disMale, disFemale, disMale + disFemale),
                byShift,
                withInclusion,
                totalBeneficiaries);
        }
        else
        {
            var releasedList = await dbContext.Internships
                .Include(i => i.Student)
                    .ThenInclude(st => st!.Career)
                .Include(i => i.Company)
                .AsNoTracking()
                .Where(i => periodIds.Contains(i.PeriodId) && (i.Status == "released" || i.IsExpedientComplete))
                .ToListAsync(cancellationToken);

            var byCareerAndGender = new List<Report911CareerGenderRow>();
            foreach (var c in careers)
            {
                foreach (var sector in AllSectors)
                {
                    var group = releasedList
                        .Where(i => i.Student?.CareerId == c.Id && (i.Company?.Sector ?? "private") == sector)
                        .ToList();

                    var m = group.Count(i => i.Student?.Gender == "M");
                    var f = group.Count(i => i.Student?.Gender == "F");
                    var u = group.Count - m - f;
                    var dis = group.Count(i => i.Student?.HasDisability == true);

                    byCareerAndGender.Add(new Report911CareerGenderRow(c.Code, sector, m, f, u, group.Count, dis));
                }
            }

            var bySector = AllSectors.Select(sector =>
            {
                var group = releasedList.Where(i => (i.Company?.Sector ?? "private") == sector).ToList();
                var m = group.Count(i => i.Student?.Gender == "M");
                var f = group.Count(i => i.Student?.Gender == "F");
                var dis = group.Count(i => i.Student?.HasDisability == true);
                return new Report911SectorRow(sector, m, f, group.Count, dis);
            }).ToList();

            var bySize = releasedList
                .GroupBy(i => i.Company?.CompanySize ?? "medium")
                .Select(g => new Report911CompanySizeRow(g.Key, g.Count()))
                .ToList();

            var disMale = releasedList.Count(i => i.Student?.HasDisability == true && i.Student?.Gender == "M");
            var disFemale = releasedList.Count(i => i.Student?.HasDisability == true && i.Student?.Gender == "F");

            var byShift = releasedList
                .GroupBy(i => i.Student?.Shift ?? "morning")
                .Select(g => new Report911ShiftRow(g.Key, g.Count()))
                .ToList();

            return new Report911Response(
                new ReportPeriodSummary(period.Id, period.Code, period.Name),
                normType,
                byCareerAndGender,
                bySector,
                bySize,
                new Report911DisabilitySummary(disMale, disFemale, disMale + disFemale),
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
        var stats = await GetStatisticsAsync(periodId, null, normType, cancellationToken);

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

