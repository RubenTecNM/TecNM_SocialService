using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecNM.Api.Core.Http;

namespace TecNM.Api.Modules.Companies;

[ApiController]
[Route("api/companies/{companyId:long}/supervisors")]
public class SupervisorsController(SupervisorsService supervisorsService) : ControllerBase
{
    private const string StaffAndDeptRoles = "department_head,social_service_officer,internship_coordinator";
    private const string StaffRoles = "department_head,social_service_officer,internship_coordinator";

    [Authorize(Roles = StaffAndDeptRoles)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CompanySupervisorResponse>>> GetByCompany(
        long companyId,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var result = await supervisorsService.GetByCompanyAsync(companyId, isActive, cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [HttpGet("options")]
    public async Task<ActionResult<IReadOnlyList<OptionItem>>> GetOptions(
        long companyId,
        CancellationToken cancellationToken = default)
    {
        var result = await supervisorsService.GetOptionsByCompanyAsync(companyId, cancellationToken);
        return Ok(result);
    }

    [Authorize(Roles = StaffAndDeptRoles)]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<CompanySupervisorResponse>> GetById(
        long companyId,
        long id,
        CancellationToken cancellationToken = default)
    {
        var supervisor = await supervisorsService.GetByIdAsync(companyId, id, cancellationToken);
        return Ok(supervisor);
    }

    [Authorize(Roles = StaffRoles)]
    [HttpPost]
    public async Task<ActionResult<CompanySupervisorResponse>> Create(
        long companyId,
        [FromBody] CompanySupervisorRequest request,
        CancellationToken cancellationToken = default)
    {
        var created = await supervisorsService.CreateAsync(companyId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { companyId, id = created.Id }, created);
    }

    [Authorize(Roles = StaffRoles)]
    [HttpPut("{id:long}")]
    public async Task<ActionResult<CompanySupervisorResponse>> Update(
        long companyId,
        long id,
        [FromBody] CompanySupervisorRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await supervisorsService.UpdateAsync(companyId, id, request, cancellationToken);
        return Ok(updated);
    }

    [Authorize(Roles = StaffRoles)]
    [HttpPatch("{id:long}/status")]
    public async Task<ActionResult<CompanySupervisorResponse>> UpdateStatus(
        long companyId,
        long id,
        [FromBody] UpdateStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await supervisorsService.UpdateStatusAsync(companyId, id, request.IsActive, cancellationToken);
        return Ok(updated);
    }

    [Authorize(Roles = StaffRoles)]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(
        long companyId,
        long id,
        CancellationToken cancellationToken = default)
    {
        await supervisorsService.DeleteAsync(companyId, id, cancellationToken);
        return NoContent();
    }
}
