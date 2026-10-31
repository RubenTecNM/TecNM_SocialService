using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TecNM.Api.Core.Http;

namespace TecNM.Api.Modules.Expedients;

[ApiController]
[Authorize]
public sealed class DocumentsController(DocumentsService service) : ControllerBase
{
    [HttpGet("/api/students/{studentId:long}/documents")]
    [Authorize(Roles = "admin,social_service_officer,internship_coordinator,department_head,student")]
    public async Task<ActionResult<IReadOnlyList<ExpedientDocumentResponse>>> GetStudentDocuments(
        long studentId,
        [FromQuery] string? tramiteType = null,
        [FromQuery] long? tramiteId = null,
        [FromQuery] string? status = null,
        [FromQuery] bool includeSuperseded = false,
        CancellationToken cancellationToken = default)
    {
        var result = await service.GetStudentDocumentsAsync(
            studentId,
            tramiteType,
            tramiteId,
            status,
            includeSuperseded,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("/api/students/me/documents")]
    [Authorize(Roles = "student")]
    public async Task<ActionResult<IReadOnlyList<ExpedientDocumentResponse>>> GetMyDocuments(
        [FromQuery] string? tramiteType = null,
        [FromQuery] long? tramiteId = null,
        [FromQuery] string? status = null,
        [FromQuery] bool includeSuperseded = false,
        CancellationToken cancellationToken = default)
    {
        var result = await service.GetMyDocumentsAsync(
            tramiteType,
            tramiteId,
            status,
            includeSuperseded,
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("/api/students/{studentId:long}/documents")]
    [Authorize(Roles = "admin,social_service_officer,internship_coordinator,student")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ExpedientDocumentResponse>> UploadStudentDocument(
        long studentId,
        [FromForm] UploadDocumentFormRequest request,
        CancellationToken cancellationToken = default)
    {
        var created = await service.UploadDocumentAsync(
            studentId,
            request.File,
            request.TramiteType,
            request.TramiteId,
            request.DocumentCode,
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPost("/api/students/me/documents")]
    [Authorize(Roles = "student")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ExpedientDocumentResponse>> UploadMyDocument(
        [FromForm] UploadDocumentFormRequest request,
        CancellationToken cancellationToken = default)
    {
        var created = await service.UploadMyDocumentAsync(
            request.File,
            request.TramiteType,
            request.TramiteId,
            request.DocumentCode,
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpGet("/api/documents/{id:long}/file")]
    [Authorize(Roles = "admin,social_service_officer,internship_coordinator,department_head,student")]
    public async Task<IActionResult> DownloadFile(long id, CancellationToken cancellationToken = default)
    {
        var (stream, fileName) = await service.GetDocumentStreamAsync(id, cancellationToken);
        return File(stream, "application/pdf", fileName);
    }

    [HttpPost("/api/documents/{id:long}/review")]
    [Authorize(Roles = "admin,social_service_officer,internship_coordinator")]
    public async Task<ActionResult<ExpedientDocumentResponse>> ReviewDocument(
        long id,
        [FromBody] ReviewDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var reviewed = await service.ReviewDocumentAsync(id, request, cancellationToken);
        return Ok(reviewed);
    }

    [HttpDelete("/api/documents/{id:long}")]
    [Authorize(Roles = "admin,social_service_officer,internship_coordinator,student")]
    public async Task<IActionResult> DeleteDocument(long id, CancellationToken cancellationToken = default)
    {
        await service.DeleteDocumentAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("/api/documents/review-queue")]
    [Authorize(Roles = "admin,social_service_officer,internship_coordinator")]
    public async Task<ActionResult<PagedResponse<ReviewQueueDocumentResponse>>> GetReviewQueue(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? tramiteType = null,
        [FromQuery] string? documentCode = null,
        [FromQuery] long? periodId = null,
        [FromQuery] string? status = "pending",
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await service.GetReviewQueueAsync(
            page,
            pageSize,
            tramiteType,
            documentCode,
            periodId,
            status,
            search,
            cancellationToken);

        return Ok(result);
    }
}

