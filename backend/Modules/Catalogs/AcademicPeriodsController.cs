using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecNM.Api.Core.Http;

namespace TecNM.Api.Modules.Catalogs;

[ApiController]
[Route("api/academic-periods")]
public class AcademicPeriodsController(AcademicPeriodsService academicPeriodsService) : ControllerBase
{
    [Authorize]
    [HttpGet]
    public async Task<ActionResult<PagedResponse<AcademicPeriodResponse>>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? sort = null,
        CancellationToken cancellationToken = default)
    {
        var result = await academicPeriodsService.GetPagedAsync(page, pageSize, search, isActive, sort, cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [HttpGet("options")]
    public async Task<ActionResult<IReadOnlyList<OptionItem>>> GetOptions(CancellationToken cancellationToken = default)
    {
        var options = await academicPeriodsService.GetOptionsAsync(cancellationToken);
        return Ok(options);
    }

    [Authorize]
    [HttpGet("school-years")]
    public async Task<ActionResult<IReadOnlyList<SchoolYearOptionResponse>>> GetSchoolYears(CancellationToken cancellationToken = default)
    {
        var years = await academicPeriodsService.GetSchoolYearsAsync(cancellationToken);
        return Ok(years);
    }

    [Authorize]
    [HttpGet("current")]
    public async Task<ActionResult<AcademicPeriodResponse>> GetCurrent(CancellationToken cancellationToken = default)
    {
        var current = await academicPeriodsService.GetCurrentAsync(cancellationToken);
        return Ok(current);
    }

    [Authorize]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<AcademicPeriodResponse>> GetById(long id, CancellationToken cancellationToken = default)
    {
        var period = await academicPeriodsService.GetByIdAsync(id, cancellationToken);
        return Ok(period);
    }

    [Authorize(Roles = "admin")]
    [HttpPost]
    public async Task<ActionResult<AcademicPeriodResponse>> Create(
        [FromBody] AcademicPeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var created = await academicPeriodsService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Roles = "admin")]
    [HttpPut("{id:long}")]
    public async Task<ActionResult<AcademicPeriodResponse>> Update(
        long id,
        [FromBody] AcademicPeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await academicPeriodsService.UpdateAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [Authorize(Roles = "admin")]
    [HttpPost("{id:long}/activate")]
    public async Task<ActionResult<AcademicPeriodResponse>> Activate(
        long id,
        CancellationToken cancellationToken = default)
    {
        var activated = await academicPeriodsService.ActivateAsync(id, cancellationToken);
        return Ok(activated);
    }

    [Authorize(Roles = "admin")]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken = default)
    {
        await academicPeriodsService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
