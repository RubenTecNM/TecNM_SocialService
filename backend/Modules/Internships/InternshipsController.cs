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
    [Authorize(Roles = "admin,internship_coordinator,social_service_officer,department_head")]
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
    [Authorize(Roles = "admin,internship_coordinator,social_service_officer,department_head,student")]
    public async Task<ActionResult<InternshipDetailResponse>> GetById(
        long id,
        CancellationToken cancellationToken = default)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "admin,internship_coordinator,student")]
    public async Task<ActionResult<InternshipDetailResponse>> Create(
        [FromBody] CreateInternshipRequest request,
        CancellationToken cancellationToken = default)
    {
        var created = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    [Authorize(Roles = "admin,internship_coordinator,student")]
    public async Task<ActionResult<InternshipDetailResponse>> Update(
        long id,
        [FromBody] CreateInternshipRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.UpdateAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:long}/approve")]
    [Authorize(Roles = "admin,internship_coordinator")]
    public async Task<ActionResult<InternshipDetailResponse>> Approve(
        long id,
        [FromBody] ApproveInternshipRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.ApproveAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:long}/presentation-letter")]
    [Authorize(Roles = "admin,internship_coordinator")]
    public async Task<ActionResult<InternshipDetailResponse>> RegisterPresentationLetter(
        long id,
        [FromBody] InternshipPresentationLetterRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.RegisterPresentationLetterAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:long}/acceptance")]
    [Authorize(Roles = "admin,internship_coordinator,student")]
    public async Task<ActionResult<InternshipDetailResponse>> RegisterAcceptance(
        long id,
        [FromBody] RegisterInternshipAcceptanceRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.RegisterAcceptanceAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:long}/activate")]
    [Authorize(Roles = "admin,internship_coordinator")]
    public async Task<ActionResult<InternshipDetailResponse>> Activate(
        long id,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.ActivateAsync(id, cancellationToken);
        return Ok(updated);
    }

    [HttpPut("{id:long}/evaluation")]
    [Authorize(Roles = "admin,internship_coordinator,student")]
    public async Task<ActionResult<InternshipEvaluationResponse>> UpsertEvaluation(
        long id,
        [FromBody] InternshipEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        var evaluation = await service.UpsertEvaluationAsync(id, request, cancellationToken);
        return Ok(evaluation);
    }

    [HttpPatch("{id:long}/expedient")]
    [Authorize(Roles = "admin,internship_coordinator")]
    public async Task<ActionResult<InternshipDetailResponse>> UpdateExpedient(
        long id,
        [FromBody] UpdateInternshipExpedientRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.UpdateExpedientAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:long}/complete")]
    [Authorize(Roles = "admin,internship_coordinator")]
    public async Task<ActionResult<InternshipDetailResponse>> Complete(
        long id,
        [FromBody] CompleteInternshipRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.CompleteAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:long}/release")]
    [Authorize(Roles = "admin,internship_coordinator")]
    public async Task<ActionResult<InternshipDetailResponse>> Release(
        long id,
        [FromBody] ReleaseInternshipRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.ReleaseAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:long}/school-services-act")]
    [Authorize(Roles = "admin,internship_coordinator")]
    public async Task<ActionResult<InternshipDetailResponse>> SendSchoolServicesAct(
        long id,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.SendSchoolServicesActAsync(id, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:long}/cancel")]
    [Authorize(Roles = "admin,internship_coordinator,student")]
    public async Task<ActionResult<InternshipDetailResponse>> Cancel(
        long id,
        [FromBody] CancelTramiteRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.CancelAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:long}/non-compliance")]
    [Authorize(Roles = "admin,internship_coordinator")]
    public async Task<ActionResult<InternshipDetailResponse>> MarkNonCompliance(
        long id,
        [FromBody] MarkNonComplianceRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.MarkNonComplianceAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpGet("{id:long}/history")]
    [Authorize(Roles = "admin,internship_coordinator,social_service_officer,department_head,student")]
    public async Task<ActionResult<IReadOnlyList<StatusLogItemResponse>>> GetHistory(
        long id,
        CancellationToken cancellationToken = default)
    {
        var history = await service.GetHistoryAsync(id, cancellationToken);
        return Ok(history);
    }

    [HttpGet("{id:long}/print-data/{document}")]
    [Authorize(Roles = "admin,internship_coordinator,social_service_officer,department_head,student")]
    public async Task<ActionResult<Dictionary<string, object?>>> GetPrintData(
        long id,
        string document,
        CancellationToken cancellationToken = default)
    {
        var printData = await service.GetPrintDataAsync(id, document, cancellationToken);
        return Ok(printData);
    }
}
