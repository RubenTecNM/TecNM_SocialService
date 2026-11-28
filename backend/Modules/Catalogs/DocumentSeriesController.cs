using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TecNM.Api.Modules.Catalogs;

[ApiController]
[Route("api/document-series")]
public class DocumentSeriesController(DocumentSeriesService seriesService) : ControllerBase
{
    [Authorize(Roles = "department_head,social_service_officer,internship_coordinator")]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DocumentSeriesResponse>>> GetList(
        [FromQuery] string? documentType = null,
        [FromQuery] int? currentYear = null,
        CancellationToken cancellationToken = default)
    {
        var list = await seriesService.GetListAsync(documentType, currentYear, cancellationToken);
        return Ok(list);
    }

    [Authorize(Roles = "department_head,social_service_officer,internship_coordinator")]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<DocumentSeriesResponse>> GetById(
        long id,
        CancellationToken cancellationToken = default)
    {
        var series = await seriesService.GetByIdAsync(id, cancellationToken);
        return Ok(series);
    }

    [Authorize(Roles = "department_head")]
    [HttpPost]
    public async Task<ActionResult<DocumentSeriesResponse>> Create(
        [FromBody] DocumentSeriesRequest request,
        CancellationToken cancellationToken = default)
    {
        var created = await seriesService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Roles = "department_head")]
    [HttpPut("{id:long}")]
    public async Task<ActionResult<DocumentSeriesResponse>> Update(
        long id,
        [FromBody] DocumentSeriesRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await seriesService.UpdateAsync(id, request, cancellationToken);
        return Ok(updated);
    }
}
