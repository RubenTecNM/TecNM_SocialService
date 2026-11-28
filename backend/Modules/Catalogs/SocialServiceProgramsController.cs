using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecNM.Api.Core.Http;

namespace TecNM.Api.Modules.Catalogs;

[ApiController]
[Route("api/social-service-programs")]
public class SocialServiceProgramsController(SocialServiceProgramsService programsService) : ControllerBase
{
    [Authorize]
    [HttpGet]
    public async Task<ActionResult<PagedResponse<SocialServiceProgramResponse>>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] bool? isInternal = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? sort = null,
        CancellationToken cancellationToken = default)
    {
        var result = await programsService.GetPagedAsync(
            page, pageSize, search, category, isInternal, isActive, sort, cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [HttpGet("options")]
    public async Task<ActionResult<IReadOnlyList<OptionItem>>> GetOptions(CancellationToken cancellationToken = default)
    {
        var options = await programsService.GetOptionsAsync(cancellationToken);
        return Ok(options);
    }

    [Authorize]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<SocialServiceProgramResponse>> GetById(
        long id,
        CancellationToken cancellationToken = default)
    {
        var program = await programsService.GetByIdAsync(id, cancellationToken);
        return Ok(program);
    }

    [Authorize(Roles = "department_head,social_service_officer")]
    [HttpPost]
    public async Task<ActionResult<SocialServiceProgramResponse>> Create(
        [FromBody] SocialServiceProgramRequest request,
        CancellationToken cancellationToken = default)
    {
        var created = await programsService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Roles = "department_head,social_service_officer")]
    [HttpPut("{id:long}")]
    public async Task<ActionResult<SocialServiceProgramResponse>> Update(
        long id,
        [FromBody] SocialServiceProgramRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await programsService.UpdateAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [Authorize(Roles = "department_head,social_service_officer")]
    [HttpPatch("{id:long}/status")]
    public async Task<ActionResult<SocialServiceProgramResponse>> UpdateStatus(
        long id,
        [FromBody] UpdateStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await programsService.UpdateStatusAsync(id, request.IsActive, cancellationToken);
        return Ok(updated);
    }

    [Authorize(Roles = "department_head,social_service_officer")]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(
        long id,
        CancellationToken cancellationToken = default)
    {
        await programsService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
