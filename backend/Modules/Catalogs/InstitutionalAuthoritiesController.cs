using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecNM.Api.Core.Http;
using TecNM.Api.Modules.Expedients.Templates;

namespace TecNM.Api.Modules.Catalogs;

[ApiController]
[Route("api/institutional-authorities")]
[TemplateAntiforgery]
public class InstitutionalAuthoritiesController(InstitutionalAuthoritiesService authoritiesService) : ControllerBase
{
    [Authorize(Roles = "department_head,social_service_officer,internship_coordinator")]
    [HttpGet]
    public async Task<ActionResult<PagedResponse<InstitutionalAuthorityResponse>>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? roleKey = null,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var result = await authoritiesService.GetPagedAsync(page, pageSize, search, roleKey, isActive, cancellationToken);
        return Ok(result);
    }

    [Authorize(Roles = "department_head,social_service_officer,internship_coordinator")]
    [HttpGet("active")]
    public async Task<ActionResult<IReadOnlyDictionary<string, InstitutionalAuthorityResponse?>>> GetActiveMap(
        CancellationToken cancellationToken = default)
    {
        var activeMap = await authoritiesService.GetActiveMapAsync(cancellationToken);
        return Ok(activeMap);
    }

    [Authorize(Roles = "department_head,social_service_officer,internship_coordinator")]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<InstitutionalAuthorityResponse>> GetById(
        long id,
        CancellationToken cancellationToken = default)
    {
        var authority = await authoritiesService.GetByIdAsync(id, cancellationToken);
        return Ok(authority);
    }

    [Authorize(Roles = "department_head")]
    [HttpPost]
    public async Task<ActionResult<InstitutionalAuthorityResponse>> Create(
        [FromBody] InstitutionalAuthorityRequest request,
        CancellationToken cancellationToken = default)
    {
        var created = await authoritiesService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Roles = "department_head")]
    [HttpPut("{id:long}")]
    public async Task<ActionResult<InstitutionalAuthorityResponse>> Update(
        long id,
        [FromBody] InstitutionalAuthorityRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await authoritiesService.UpdateAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [Authorize(Roles = "department_head")]
    [HttpPatch("{id:long}/status")]
    public async Task<ActionResult<InstitutionalAuthorityResponse>> UpdateStatus(
        long id,
        [FromBody] UpdateStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await authoritiesService.UpdateStatusAsync(id, request.IsActive, cancellationToken);
        return Ok(updated);
    }

    [Authorize(Roles = "department_head")]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(
        long id,
        CancellationToken cancellationToken = default)
    {
        await authoritiesService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
