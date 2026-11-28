using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TecNM.Api.Modules.Reports;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = "StatisticsRead")]
public sealed class ReportsController(ReportsService service) : ControllerBase
{
    [HttpGet("analytics")]
    public async Task<ActionResult<AnalyticsReportResponse>> GetAnalytics(
        [FromQuery] string tramiteType = "social_service",
        [FromQuery] string timeScope = "period",
        [FromQuery] long? periodId = null,
        [FromQuery] long? comparePeriodId = null,
        [FromQuery] string? schoolYear = null,
        [FromQuery] string? compareSchoolYear = null,
        [FromQuery] string statusGroup = "in_progress",
        [FromQuery] string? status = null,
        [FromQuery] long? careerId = null,
        [FromQuery] int? semester = null,
        [FromQuery] long? companyId = null,
        [FromQuery] string? companyType = null,
        [FromQuery] string? sector = null,
        [FromQuery] long? activityTypeId = null,
        [FromQuery] string? gender = null,
        CancellationToken cancellationToken = default)
    {
        var query = new AnalyticsFilterQuery(
            tramiteType, timeScope, periodId, comparePeriodId, schoolYear, compareSchoolYear,
            statusGroup, status, careerId, semester, companyId, companyType, sector, activityTypeId, gender);
        return Ok(await service.GetAnalyticsAsync(query, cancellationToken));
    }

    [HttpGet("analytics/companies")]
    public async Task<ActionResult<TecNM.Api.Core.Http.PagedResponse<CompanyAnalyticsRow>>> GetAnalyticsCompanies(
        [FromQuery] string tramiteType = "social_service",
        [FromQuery] string timeScope = "period",
        [FromQuery] long? periodId = null,
        [FromQuery] string? schoolYear = null,
        [FromQuery] string statusGroup = "in_progress",
        [FromQuery] string? status = null,
        [FromQuery] long? careerId = null,
        [FromQuery] int? semester = null,
        [FromQuery] long? companyId = null,
        [FromQuery] string? companyType = null,
        [FromQuery] string? sector = null,
        [FromQuery] long? activityTypeId = null,
        [FromQuery] string? gender = null,
        [FromQuery] string? search = null,
        [FromQuery] string? sort = "students_desc",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new AnalyticsFilterQuery(
            tramiteType, timeScope, periodId, null, schoolYear, null,
            statusGroup, status, careerId, semester, companyId, companyType, sector, activityTypeId, gender);
        return Ok(await service.GetAnalyticsCompaniesAsync(query, search, sort, page, pageSize, cancellationToken));
    }

    [HttpGet("linkage")]
    public async Task<ActionResult<LinkageResponse>> GetLinkage(
        [FromQuery] string tramiteType, [FromQuery] long? periodId = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => Ok(await service.GetLinkageAsync(tramiteType, periodId, page, pageSize, cancellationToken));

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardResponse>> GetDashboard(
        [FromQuery] long? periodId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await service.GetDashboardAsync(periodId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("tracking")]
    public async Task<ActionResult<IReadOnlyList<TrackingRowResponse>>> GetTracking(
        [FromQuery] string tramiteType = "social_service",
        [FromQuery] long? periodId = null,
        [FromQuery] string? schoolYear = null,
        [FromQuery] string? status = null,
        [FromQuery] long? careerId = null,
        [FromQuery] int? semester = null,
        [FromQuery] string? gender = null,
        [FromQuery] long? companyId = null,
        [FromQuery] long? activityTypeId = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await service.GetTrackingAsync(
            periodId, schoolYear, tramiteType, status, careerId, semester, gender, companyId, activityTypeId, search, cancellationToken);
        return Ok(result);
    }

    [HttpGet("statistics")]
    public async Task<ActionResult<StatisticalReportResponse>> GetStatistics(
        [FromQuery] string tramiteType = "social_service",
        [FromQuery] long? periodId = null,
        [FromQuery] long? comparePeriodId = null,
        [FromQuery] string? schoolYear = null,
        [FromQuery] string? compareSchoolYear = null,
        CancellationToken cancellationToken = default)
    {
        var result = await service.GetStatisticsAsync(periodId, comparePeriodId, schoolYear, compareSchoolYear, tramiteType, cancellationToken);
        return Ok(result);
    }

    [HttpGet("911")]
    public async Task<ActionResult<Report911Response>> GetReport911(
        [FromQuery] string tramiteType = "social_service",
        [FromQuery] long? periodId = null,
        [FromQuery] long? comparePeriodId = null,
        [FromQuery] string? schoolYear = null,
        CancellationToken cancellationToken = default)
    {
        var result = await service.GetReport911Async(periodId, comparePeriodId, schoolYear, tramiteType, cancellationToken);
        return Ok(result);
    }

    [HttpGet("non-conformance-indicator")]
    public async Task<ActionResult<NonConformanceIndicatorResponse>> GetNonConformanceIndicator(
        [FromQuery] string tramiteType = "social_service",
        [FromQuery] long? periodId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await service.GetNonConformanceIndicatorAsync(periodId, tramiteType, cancellationToken);
        return Ok(result);
    }

    [HttpGet("cycle-times")]
    public async Task<ActionResult<CycleTimesResponse>> GetCycleTimes(
        [FromQuery] string tramiteType = "social_service",
        CancellationToken cancellationToken = default)
    {
        var result = await service.GetCycleTimesAsync(tramiteType, cancellationToken);
        return Ok(result);
    }
}

