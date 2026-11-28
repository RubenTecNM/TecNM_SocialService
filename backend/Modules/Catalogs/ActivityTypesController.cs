using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecNM.Api.Core.Http;

namespace TecNM.Api.Modules.Catalogs;

[ApiController]
[Route("api/activity-types")]
public sealed class ActivityTypesController(ActivityTypesService service) : ControllerBase
{
    [Authorize]
    [HttpGet]
    public async Task<ActionResult<PagedResponse<ActivityTypeResponse>>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? appliesTo = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? sort = null,
        CancellationToken cancellationToken = default)
        => Ok(await service.GetPagedAsync(page, pageSize, search, appliesTo, isActive, sort, cancellationToken));

    [Authorize]
    [HttpGet("options")]
    public async Task<ActionResult<IReadOnlyList<OptionItem>>> GetOptions(
        [FromQuery] string? tramiteType = null,
        CancellationToken cancellationToken = default)
        => Ok(await service.GetOptionsAsync(tramiteType, cancellationToken));

    [Authorize]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<ActivityTypeResponse>> GetById(
        long id,
        CancellationToken cancellationToken = default)
        => Ok(await service.GetByIdAsync(id, cancellationToken));

    [Authorize(Policy = "CompaniesWrite")]
    [HttpPost]
    public async Task<ActionResult<ActivityTypeResponse>> Create(
        [FromBody] ActivityTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        var created = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Policy = "CompaniesWrite")]
    [HttpPut("{id:long}")]
    public async Task<ActionResult<ActivityTypeResponse>> Update(
        long id,
        [FromBody] ActivityTypeRequest request,
        CancellationToken cancellationToken = default)
        => Ok(await service.UpdateAsync(id, request, cancellationToken));

    [Authorize(Policy = "CompaniesWrite")]
    [HttpPatch("{id:long}/status")]
    public async Task<ActionResult<ActivityTypeResponse>> UpdateStatus(
        long id,
        [FromBody] UpdateStatusRequest request,
        CancellationToken cancellationToken = default)
        => Ok(await service.UpdateStatusAsync(id, request.IsActive, cancellationToken));

    [Authorize(Policy = "CompaniesWrite")]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken = default)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
