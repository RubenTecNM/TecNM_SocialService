using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TecNM.Api.Core.Http;

namespace TecNM.Api.Modules.Students;

[ApiController]
[Route("api/students")]
public class StudentsController(StudentsService studentsService) : ControllerBase
{
    private const string StaffAndDeptRoles = "admin,social_service_officer,internship_coordinator,department_head";
    private const string StaffRoles = "admin,social_service_officer,internship_coordinator";

    [Authorize(Roles = StaffAndDeptRoles)]
    [HttpGet]
    public async Task<ActionResult<PagedResponse<StudentListItem>>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] long? careerId = null,
        [FromQuery] string? shift = null,
        [FromQuery] bool? isEnrolled = null,
        [FromQuery] bool? hasValidImss = null,
        [FromQuery] bool? hasDisability = null,
        [FromQuery] string? sort = null,
        CancellationToken cancellationToken = default)
    {
        var result = await studentsService.GetPagedAsync(
            page, pageSize, search, careerId, shift, isEnrolled, hasValidImss, hasDisability, sort, cancellationToken);
        return Ok(result);
    }

    [Authorize(Roles = StaffAndDeptRoles)]
    [HttpGet("options")]
    public async Task<ActionResult<IReadOnlyList<OptionItem>>> GetOptions(
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var options = await studentsService.GetOptionsAsync(search, cancellationToken);
        return Ok(options);
    }

    [Authorize(Roles = "student")]
    [HttpGet("me")]
    public async Task<ActionResult<StudentDetail>> GetMe(CancellationToken cancellationToken = default)
    {
        var detail = await studentsService.GetMyProfileAsync(cancellationToken);
        return Ok(detail);
    }

    [Authorize(Roles = "student")]
    [HttpPut("me/contact")]
    public async Task<ActionResult<StudentDetail>> UpdateMyContact(
        [FromBody] StudentContactRequest request,
        CancellationToken cancellationToken = default)
    {
        var detail = await studentsService.UpdateMyContactAsync(request, cancellationToken);
        return Ok(detail);
    }

    [Authorize(Roles = "student")]
    [HttpPut("me/imss")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<StudentDetail>> UploadMyImss(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        var detail = await studentsService.UploadMyImssAsync(file, cancellationToken);
        return Ok(detail);
    }

    [Authorize(Roles = "student")]
    [HttpGet("me/imss")]
    public async Task<IActionResult> GetMyImss(CancellationToken cancellationToken = default)
    {
        var (stream, contentType, fileName) = await studentsService.GetMyImssStreamAsync(cancellationToken);
        return File(stream, contentType, fileName);
    }

    [Authorize(Roles = "student")]
    [HttpGet("me/eligibility")]
    public async Task<ActionResult<StudentEligibility>> GetMyEligibility(CancellationToken cancellationToken = default)
    {
        var eligibility = await studentsService.GetMyEligibilityAsync(cancellationToken);
        return Ok(eligibility);
    }

    [Authorize(Roles = StaffAndDeptRoles)]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<StudentDetail>> GetById(long id, CancellationToken cancellationToken = default)
    {
        var detail = await studentsService.GetByIdAsync(id, cancellationToken);
        return Ok(detail);
    }

    [Authorize(Roles = StaffRoles)]
    [HttpPost]
    public async Task<ActionResult<StudentDetail>> Create(
        [FromBody] StudentRequest request,
        CancellationToken cancellationToken = default)
    {
        var created = await studentsService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Roles = StaffRoles)]
    [HttpPut("{id:long}")]
    public async Task<ActionResult<StudentDetail>> Update(
        long id,
        [FromBody] StudentRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await studentsService.UpdateAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [Authorize(Roles = StaffRoles)]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken = default)
    {
        await studentsService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = StaffAndDeptRoles)]
    [HttpGet("{id:long}/eligibility")]
    public async Task<ActionResult<StudentEligibility>> GetEligibility(
        long id,
        CancellationToken cancellationToken = default)
    {
        var eligibility = await studentsService.GetEligibilityAsync(id, cancellationToken);
        return Ok(eligibility);
    }

    [Authorize(Roles = StaffRoles)]
    [HttpPost("{id:long}/account")]
    public async Task<ActionResult<CreateStudentAccountResponse>> CreateAccount(
        long id,
        CancellationToken cancellationToken = default)
    {
        var account = await studentsService.CreateAccountAsync(id, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, account);
    }

    [Authorize(Roles = StaffRoles)]
    [HttpPut("{id:long}/imss")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<StudentDetail>> UploadImss(
        long id,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        var detail = await studentsService.UploadImssAsync(id, file, cancellationToken);
        return Ok(detail);
    }

    [Authorize(Roles = StaffAndDeptRoles)]
    [HttpGet("{id:long}/imss")]
    public async Task<IActionResult> GetImss(long id, CancellationToken cancellationToken = default)
    {
        var (stream, contentType, fileName) = await studentsService.GetImssStreamAsync(id, cancellationToken);
        return File(stream, contentType, fileName);
    }

    [Authorize(Roles = StaffRoles)]
    [HttpPost("{id:long}/imss/validation")]
    public async Task<ActionResult<StudentDetail>> ValidateImss(
        long id,
        [FromBody] ValidateImssRequest request,
        CancellationToken cancellationToken = default)
    {
        var detail = await studentsService.ValidateImssAsync(id, request.IsValid, cancellationToken);
        return Ok(detail);
    }
}

