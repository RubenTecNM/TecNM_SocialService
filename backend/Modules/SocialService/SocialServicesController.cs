using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TecNM.Api.Core.Http;

namespace TecNM.Api.Modules.SocialService;

[ApiController]
[Route("api/social-services")]
[Authorize]
public sealed class SocialServicesController(SocialServiceService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "admin,social_service_officer,internship_coordinator,department_head")]
    public async Task<ActionResult<PagedResponse<SocialServiceListItemResponse>>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] long? periodId = null,
        [FromQuery] string? status = null,
        [FromQuery] long? careerId = null,
        [FromQuery] long? companyId = null,
        [FromQuery] long? programId = null,
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
            programId,
            search,
            sort,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("me")]
    [Authorize(Roles = "student")]
    public async Task<ActionResult<IReadOnlyList<SocialServiceListItemResponse>>> GetMyList(
        CancellationToken cancellationToken = default)
    {
        var result = await service.GetMyListAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    [Authorize(Roles = "admin,social_service_officer,internship_coordinator,department_head,student")]
    public async Task<ActionResult<SocialServiceDetailResponse>> GetById(
        long id,
        CancellationToken cancellationToken = default)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "admin,social_service_officer,student")]
    public async Task<ActionResult<SocialServiceDetailResponse>> Create(
        [FromBody] CreateSocialServiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var created = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    [Authorize(Roles = "admin,social_service_officer,student")]
    public async Task<ActionResult<SocialServiceDetailResponse>> Update(
        long id,
        [FromBody] UpdateSocialServiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.UpdateAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:long}/application/review")]
    [Authorize(Roles = "admin,social_service_officer")]
    public async Task<ActionResult<SocialServiceDetailResponse>> ReviewApplication(
        long id,
        [FromBody] ReviewApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.ReviewApplicationAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:long}/presentation-letter")]
    [Authorize(Roles = "admin,social_service_officer")]
    public async Task<ActionResult<SocialServiceDetailResponse>> RegisterPresentationLetter(
        long id,
        [FromBody] PresentationLetterRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.RegisterPresentationLetterAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:long}/acceptance")]
    [Authorize(Roles = "admin,social_service_officer")]
    public async Task<ActionResult<SocialServiceDetailResponse>> RegisterAcceptance(
        long id,
        [FromBody] AcceptanceLetterRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.RegisterAcceptanceAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPut("{id:long}/commitment")]
    [Authorize(Roles = "admin,social_service_officer,student")]
    public async Task<ActionResult<SocialServiceDetailResponse>> UpsertCommitment(
        long id,
        [FromBody] CommitmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.UpsertCommitmentAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:long}/activate")]
    [Authorize(Roles = "admin,social_service_officer")]
    public async Task<ActionResult<SocialServiceDetailResponse>> Activate(
        long id,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.ActivateAsync(id, cancellationToken);
        return Ok(updated);
    }

    [HttpPut("{id:long}/reports/{reportNumber:int}")]
    [Authorize(Roles = "admin,social_service_officer,student")]
    public async Task<ActionResult<BiMonthlyReportResponse>> UpsertReport(
        long id,
        int reportNumber,
        [FromBody] ReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var report = await service.UpsertReportAsync(id, reportNumber, request, cancellationToken);
        return Ok(report);
    }

    [HttpPost("{id:long}/reports/{reportId:long}/review")]
    [Authorize(Roles = "admin,social_service_officer")]
    public async Task<ActionResult<BiMonthlyReportResponse>> ReviewReport(
        long id,
        long reportId,
        [FromBody] ReviewReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var report = await service.ReviewReportAsync(id, reportId, request, cancellationToken);
        return Ok(report);
    }

    [HttpPut("{id:long}/evaluations/{evaluatorType}/{bimonthlyPeriod}")]
    [Authorize(Roles = "admin,social_service_officer,student")]
    public async Task<ActionResult<SocialServiceEvaluationResponse>> UpsertEvaluation(
        long id,
        string evaluatorType,
        string bimonthlyPeriod,
        [FromBody] EvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        var eval = await service.UpsertEvaluationAsync(id, evaluatorType, bimonthlyPeriod, request, cancellationToken);
        return Ok(eval);
    }

    [HttpPut("{id:long}/activity-assessment")]
    [Authorize(Roles = "admin,social_service_officer,student")]
    public async Task<ActionResult<ActivityAssessmentResponse>> UpsertActivityAssessment(
        long id,
        [FromBody] ActivityAssessmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await service.UpsertActivityAssessmentAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{id:long}/impact")]
    [Authorize(Roles = "admin,social_service_officer")]
    public async Task<ActionResult<SocialServiceDetailResponse>> UpdateImpact(
        long id,
        [FromBody] UpdateImpactRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.UpdateImpactAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:long}/complete")]
    [Authorize(Roles = "admin,social_service_officer")]
    public async Task<ActionResult<SocialServiceDetailResponse>> Complete(
        long id,
        [FromBody] CompleteSocialServiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.CompleteAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:long}/release")]
    [Authorize(Roles = "admin,social_service_officer")]
    public async Task<ActionResult<SocialServiceDetailResponse>> Release(
        long id,
        [FromBody] ReleaseSocialServiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.ReleaseAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:long}/school-services-act")]
    [Authorize(Roles = "admin,social_service_officer")]
    public async Task<ActionResult<SocialServiceDetailResponse>> SendSchoolServicesAct(
        long id,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.SendSchoolServicesActAsync(id, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:long}/cancel")]
    [Authorize(Roles = "admin,social_service_officer,student")]
    public async Task<ActionResult<SocialServiceDetailResponse>> Cancel(
        long id,
        [FromBody] CancelTramiteRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.CancelAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:long}/non-compliance")]
    [Authorize(Roles = "admin,social_service_officer")]
    public async Task<ActionResult<SocialServiceDetailResponse>> MarkNonCompliance(
        long id,
        [FromBody] MarkNonComplianceRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await service.MarkNonComplianceAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpGet("{id:long}/history")]
    [Authorize(Roles = "admin,social_service_officer,internship_coordinator,department_head,student")]
    public async Task<ActionResult<IReadOnlyList<StatusLogItemResponse>>> GetHistory(
        long id,
        CancellationToken cancellationToken = default)
    {
        var history = await service.GetHistoryAsync(id, cancellationToken);
        return Ok(history);
    }

    [HttpGet("{id:long}/print-data/{document}")]
    [Authorize(Roles = "admin,social_service_officer,internship_coordinator,department_head,student")]
    public async Task<ActionResult<Dictionary<string, object?>>> GetPrintData(
        long id,
        string document,
        CancellationToken cancellationToken = default)
    {
        var printData = await service.GetPrintDataAsync(id, document, cancellationToken);
        return Ok(printData);
    }
}

