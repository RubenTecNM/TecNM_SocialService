using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TecNM.Api.Core.Http;

namespace TecNM.Api.Modules.Quality;

[ApiController]
[Authorize]
public sealed class QualityController(QualityService service) : ControllerBase
{
    [HttpGet("/api/tramite-status-logs")]
    [Authorize(Roles = "admin,social_service_officer,internship_coordinator,department_head,student")]
    public async Task<ActionResult<IReadOnlyList<StatusLogFullResponse>>> GetStatusLogs(
        [FromQuery] string tramiteType,
        [FromQuery] long tramiteId,
        CancellationToken cancellationToken = default)
    {
        var logs = await service.GetStatusLogsAsync(tramiteType, tramiteId, cancellationToken);
        return Ok(logs);
    }

    [HttpGet("/api/non-conformances")]
    [Authorize(Roles = "admin,social_service_officer,internship_coordinator,department_head")]
    public async Task<ActionResult<PagedResponse<NonConformanceResponse>>> GetPagedNonConformances(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] long? periodId = null,
        [FromQuery] string? tramiteType = null,
        [FromQuery] string? status = null,
        [FromQuery] string? category = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await service.GetPagedNonConformancesAsync(
            page,
            pageSize,
            periodId,
            tramiteType,
            status,
            category,
            search,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("/api/non-conformances/{id:long}")]
    [Authorize(Roles = "admin,social_service_officer,internship_coordinator,department_head")]
    public async Task<ActionResult<NonConformanceResponse>> GetNonConformanceById(
        long id,
        CancellationToken cancellationToken = default)
    {
        var result = await service.GetNonConformanceByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("/api/non-conformances")]
    [Authorize(Roles = "admin,social_service_officer,internship_coordinator")]
    public async Task<ActionResult<NonConformanceResponse>> CreateNonConformance(
        [FromBody] CreateNonConformanceRequest request,
        CancellationToken cancellationToken = default)
    {
        var created = await service.CreateNonConformanceAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetNonConformanceById), new { id = created.Id }, created);
    }

    [HttpPut("/api/non-conformances/{id:long}")]
    [Authorize(Roles = "admin,social_service_officer,internship_coordinator")]
    public async Task<ActionResult<NonConformanceResponse>> UpdateNonConformance(
        long id,
        [FromBody] UpdateNonConformanceRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.UpdateNonConformanceAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("/api/non-conformances/{id:long}/status")]
    [Authorize(Roles = "admin,social_service_officer,internship_coordinator")]
    public async Task<ActionResult<NonConformanceResponse>> UpdateNonConformanceStatus(
        long id,
        [FromBody] UpdateNonConformanceStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.UpdateNonConformanceStatusAsync(id, request, cancellationToken);
        return Ok(updated);
    }
}

