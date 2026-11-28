using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecNM.Api.Core.Http;

namespace TecNM.Api.Modules.Catalogs;

[ApiController]
[Route("api/careers")]
public class CareersController(CareersService careersService) : ControllerBase
{
    [Authorize]
    [HttpGet]
    public async Task<ActionResult<PagedResponse<CareerResponse>>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? sort = null,
        CancellationToken cancellationToken = default)
    {
        var result = await careersService.GetPagedAsync(page, pageSize, search, isActive, sort, cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [HttpGet("options")]
    public async Task<ActionResult<IReadOnlyList<OptionItem>>> GetOptions(CancellationToken cancellationToken = default)
    {
        var options = await careersService.GetOptionsAsync(cancellationToken);
        return Ok(options);
    }

    [Authorize]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<CareerResponse>> GetById(long id, CancellationToken cancellationToken = default)
    {
        var career = await careersService.GetByIdAsync(id, cancellationToken);
        return Ok(career);
    }

    [Authorize(Roles = "department_head")]
    [HttpPost]
    public async Task<ActionResult<CareerResponse>> Create(
        [FromBody] CareerRequest request,
        CancellationToken cancellationToken = default)
    {
        var created = await careersService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Roles = "department_head")]
    [HttpPut("{id:long}")]
    public async Task<ActionResult<CareerResponse>> Update(
        long id,
        [FromBody] CareerRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await careersService.UpdateAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [Authorize(Roles = "department_head")]
    [HttpPatch("{id:long}/status")]
    public async Task<ActionResult<CareerResponse>> UpdateStatus(
        long id,
        [FromBody] UpdateStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await careersService.UpdateStatusAsync(id, request.IsActive, cancellationToken);
        return Ok(updated);
    }

    [Authorize(Roles = "department_head")]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken = default)
    {
        await careersService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
