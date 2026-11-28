using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TecNM.Api.Core.Http;

namespace TecNM.Api.Modules.Students;

[ApiController]
[Route("api/student-imports")]
public class StudentImportsController(StudentImportsService studentImportsService) : ControllerBase
{

    [Authorize(Roles = "department_head,social_service_officer,internship_coordinator")]
    [HttpGet("template")]
    public IActionResult DownloadTemplate()
    {
        var bytes = studentImportsService.GenerateTemplateXlsx();
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Plantilla_Importacion_Estudiantes_TecNM.xlsx");
    }

    [Authorize(Roles = "department_head,social_service_officer")]
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<StudentImportResult>> Import(
        [FromForm] long periodId,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        var result = await studentImportsService.ImportExcelAsync(periodId, file, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [Authorize(Roles = "department_head,social_service_officer")]
    [HttpGet]
    public async Task<ActionResult<PagedResponse<StudentImportBatchDto>>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] long? periodId = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var result = await studentImportsService.GetPagedAsync(page, pageSize, periodId, status, cancellationToken);
        return Ok(result);
    }

    [Authorize(Roles = "department_head,social_service_officer")]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<StudentImportResult>> GetById(
        long id,
        CancellationToken cancellationToken = default)
    {
        var result = await studentImportsService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }
}

