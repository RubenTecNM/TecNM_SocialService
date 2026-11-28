using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecNM.Api.Core.Http;
using TecNM.Api.Modules.SocialService;

namespace TecNM.Api.Modules.Internships;

[ApiController]
[Route("api/internships")]
[Authorize]
public sealed class InternshipsController(InternshipsService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "InternshipList")]
    public async Task<ActionResult<PagedResponse<InternshipListItemResponse>>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] long? periodId = null,
        [FromQuery] string? status = null,
        [FromQuery] long? careerId = null,
        [FromQuery] long? companyId = null,
        [FromQuery] bool? isExpedientComplete = null,
        [FromQuery] string? search = null,
        [FromQuery] string? sort = null,
        CancellationToken cancellationToken = default)
    {
        var result = await service.GetPagedAsync(
            page,
            pageSize,
            periodId,
            status,
            careerId,
            companyId,
            isExpedientComplete,
            search,
            sort,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("me")]
    [Authorize(Roles = "student")]
    public async Task<ActionResult<IReadOnlyList<InternshipListItemResponse>>> GetMyList(
        CancellationToken cancellationToken = default)
    {
        var result = await service.GetMyListAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = "InternshipRead")]
    public async Task<ActionResult<InternshipDetailResponse>> GetById(
        long id,
        CancellationToken cancellationToken = default)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "department_head,internship_coordinator,student")]
    public async Task<ActionResult<InternshipDetailResponse>> Create(
        [FromBody] CreateInternshipRequest request,
        CancellationToken cancellationToken = default)
    {
        var created = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    [Authorize(Roles = "department_head,internship_coordinator,student")]
    public async Task<ActionResult<InternshipDetailResponse>> Update(
        long id,
        [FromBody] CreateInternshipRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.UpdateAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:long}/approve")]
    [Authorize(Policy = "InternshipManage")]
    public async Task<ActionResult<InternshipDetailResponse>> Approve(
        long id,
        [FromBody] ApproveInternshipRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.ApproveAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:long}/presentation-letter")]
    [Authorize(Policy = "InternshipManage")]
    public async Task<ActionResult<InternshipDetailResponse>> RegisterPresentationLetter(
        long id,
        [FromBody] InternshipPresentationLetterRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.RegisterPresentationLetterAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:long}/acceptance")]
    [Authorize(Roles = "department_head,internship_coordinator,student")]
    public async Task<ActionResult<InternshipDetailResponse>> RegisterAcceptance(
        long id,
        [FromBody] RegisterInternshipAcceptanceRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.RegisterAcceptanceAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:long}/activate")]
    [Authorize(Policy = "InternshipManage")]
    public async Task<ActionResult<InternshipDetailResponse>> Activate(
        long id,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.ActivateAsync(id, cancellationToken);
        return Ok(updated);
    }

    [HttpPut("{id:long}/evaluation")]
    [Authorize(Policy = "InternshipManage")]
    public async Task<ActionResult<InternshipEvaluationResponse>> UpsertEvaluation(
        long id,
        [FromBody] InternshipEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        var evaluation = await service.UpsertEvaluationAsync(id, request, cancellationToken);
        return Ok(evaluation);
    }

    [HttpPatch("{id:long}/expedient")]
    [Authorize(Policy = "InternshipManage")]
    public async Task<ActionResult<InternshipDetailResponse>> UpdateExpedient(
        long id,
        [FromBody] UpdateInternshipExpedientRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.UpdateExpedientAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:long}/complete")]
    [Authorize(Policy = "InternshipManage")]
    public async Task<ActionResult<InternshipDetailResponse>> Complete(
        long id,
        [FromBody] CompleteInternshipRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.CompleteAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:long}/release")]
    [Authorize(Policy = "InternshipManage")]
    public async Task<ActionResult<InternshipDetailResponse>> Release(
        long id,
        [FromBody] ReleaseInternshipRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.ReleaseAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:long}/school-services-act")]
    [Authorize(Policy = "InternshipManage")]
    public async Task<ActionResult<InternshipDetailResponse>> SendSchoolServicesAct(
        long id,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.SendSchoolServicesActAsync(id, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:long}/cancel")]
    [Authorize(Roles = "department_head,internship_coordinator,student")]
    public async Task<ActionResult<InternshipDetailResponse>> Cancel(
        long id,
        [FromBody] CancelTramiteRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.CancelAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:long}/non-compliance")]
    [Authorize(Policy = "InternshipManage")]
    public async Task<ActionResult<InternshipDetailResponse>> MarkNonCompliance(
        long id,
        [FromBody] MarkNonComplianceRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.MarkNonComplianceAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPut("{id:long}/evaluation-format")]
    [Authorize(Roles = "department_head,internship_coordinator,student")]
    public async Task<IActionResult> SaveEvaluationFormat(long id, EvaluationFormatRequest request, CancellationToken ct)
    {
        await service.SaveEvaluationFormatAsync(id, request, ct);
        return NoContent();
    }

    [HttpGet("{id:long}/history")]
    [Authorize(Policy = "InternshipRead")]
    public async Task<ActionResult<IReadOnlyList<StatusLogItemResponse>>> GetHistory(
        long id,
        CancellationToken cancellationToken = default)
    {
        var history = await service.GetHistoryAsync(id, cancellationToken);
        return Ok(history);
    }

    [HttpGet("{id:long}/print-data/{document}")]
    [Authorize(Policy = "InternshipRead")]
    public async Task<ActionResult<Dictionary<string, object?>>> GetPrintData(
        long id,
        string document,
        CancellationToken cancellationToken = default)
    {
        var printData = await service.GetPrintDataAsync(id, document, cancellationToken);
        return Ok(printData);
    }
}
