using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TecNM.Api.Core.Http;

namespace TecNM.Api.Modules.Companies;

[ApiController]
[Route("api/agreements")]
public class AgreementsController(AgreementsService agreementsService) : ControllerBase
{
    private const string StaffAndDeptRoles = "department_head,social_service_officer,internship_coordinator";
    private const string StaffRoles = "department_head,social_service_officer,internship_coordinator";

    [Authorize(Roles = StaffAndDeptRoles)]
    [HttpGet]
    public async Task<ActionResult<PagedResponse<AgreementResponse>>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] long? companyId = null,
        [FromQuery] string? agreementType = null,
        [FromQuery] string? validity = null,
        [FromQuery] string? search = null,
        [FromQuery] string? sort = null,
        CancellationToken cancellationToken = default)
    {
        var result = await agreementsService.GetPagedAsync(
            page, pageSize, companyId, agreementType, validity, search, sort, cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [HttpGet("options")]
    public async Task<ActionResult<IReadOnlyList<OptionItem>>> GetOptions(
        [FromQuery] long? companyId = null,
        [FromQuery] string? tramiteType = null,
        [FromQuery] DateOnly? onDate = null,
        CancellationToken cancellationToken = default)
    {
        var options = await agreementsService.GetOptionsAsync(companyId, tramiteType, onDate, cancellationToken);
        return Ok(options);
    }

    [Authorize(Roles = StaffAndDeptRoles)]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<AgreementResponse>> GetById(long id, CancellationToken cancellationToken = default)
    {
        var agreement = await agreementsService.GetByIdAsync(id, cancellationToken);
        return Ok(agreement);
    }

    [Authorize(Roles = StaffRoles)]
    [HttpPost]
    public async Task<ActionResult<AgreementResponse>> Create(
        [FromBody] AgreementRequest request,
        CancellationToken cancellationToken = default)
    {
        var created = await agreementsService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Roles = StaffRoles)]
    [HttpPut("{id:long}")]
    public async Task<ActionResult<AgreementResponse>> Update(
        long id,
        [FromBody] AgreementRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await agreementsService.UpdateAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [Authorize(Roles = StaffRoles)]
    [HttpPatch("{id:long}/status")]
    public async Task<ActionResult<AgreementResponse>> UpdateStatus(
        long id,
        [FromBody] UpdateStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await agreementsService.UpdateStatusAsync(id, request.IsActive, cancellationToken);
        return Ok(updated);
    }

    [Authorize(Roles = StaffRoles)]
    [HttpPut("{id:long}/document")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<AgreementResponse>> UploadDocument(
        long id,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        var updated = await agreementsService.UploadDocumentAsync(id, file, cancellationToken);
        return Ok(updated);
    }

    [Authorize(Roles = StaffAndDeptRoles)]
    [HttpGet("{id:long}/document")]
    public async Task<IActionResult> GetDocument(long id, CancellationToken cancellationToken = default)
    {
        var (stream, fileName) = await agreementsService.GetDocumentStreamAsync(id, cancellationToken);
        return File(stream, "application/pdf", fileName);
    }

    [Authorize(Roles = StaffRoles)]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken = default)
    {
        await agreementsService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
