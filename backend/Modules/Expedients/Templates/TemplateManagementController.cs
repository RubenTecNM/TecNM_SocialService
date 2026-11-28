using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TecNM.Api.Modules.Expedients.Templates;

[ApiController]
[Route("api/template-management")]
[Authorize(Roles = "admin,social_service_officer,internship_coordinator")]
[TemplateAntiforgery]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TemplateManagementController(TemplateManagement management, TemplateBranding branding) : ControllerBase
{
    [HttpGet("csrf")]
    public IActionResult Csrf([FromServices] IAntiforgery antiforgery) =>
        Ok(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });

    [HttpGet]
    public Task<List<TemplateSummary>> List(CancellationToken ct) => management.ListAsync(ct);

    [HttpGet("logos")]
    public Task<List<LogoSummary>> Logos(CancellationToken ct) => branding.ListAsync(ct);

    [HttpGet("logos/{id:long}/image")]
    public async Task<IActionResult> LogoImage(long id, CancellationToken ct) =>
        File(await branding.ImageAsync(id, ct), "image/png");

    [HttpPost("logos")]
    [Authorize(Roles = "admin")]
    [RequestSizeLimit(2 * 1024 * 1024 + 65536)]
    public Task<LogoSummary> UploadLogo([FromForm] string name, IFormFile file, CancellationToken ct) =>
        branding.UploadAsync(name, file, ct);

    [HttpGet("{key}/versions")]
    public Task<List<TemplateVersionSummary>> History(string key, CancellationToken ct) => management.HistoryAsync(key, ct);

    [HttpGet("{key}/activations")]
    public Task<List<TemplateActivationSummary>> Activations(string key, CancellationToken ct) => management.ActivationsAsync(key, ct);

    [HttpGet("{key}/versions/{id:long}")]
    public Task<TemplateVersionDetail> Detail(string key, long id, CancellationToken ct) => management.DetailAsync(key, id, ct);

    [HttpGet("{key}/versions/{id:long}/file")]
    public async Task<IActionResult> VersionFile(string key, long id, [FromQuery] bool source, CancellationToken ct) =>
        File(await management.FileAsync(key, id, source, ct), "application/pdf", $"{key}-{id}.pdf");

    [HttpPost("{key}/draft")]
    public Task<TemplateVersionDetail> CreateDraft(string key, CancellationToken ct) => management.CreateDraftAsync(key, ct);

    [HttpPut("{key}/draft/file")]
    [RequestSizeLimit(TemplatePdfValidation.MaxBytes + 65536)]
    public Task<TemplateVersionDetail> ReplaceFile(string key, [FromQuery] uint revision, [FromQuery] bool cleanBranding, IFormFile file, CancellationToken ct) =>
        management.ReplaceFileAsync(key, revision, file, cleanBranding, ct);

    [HttpPut("{key}/draft/layout")]
    public Task<TemplateVersionDetail> SaveLayout(string key, TemplateLayoutEdit layout, CancellationToken ct) =>
        management.SaveLayoutAsync(key, layout, ct);

    [HttpPut("{key}/draft/logos")]
    public Task<TemplateVersionDetail> SaveLogos(string key, TemplateLogoEdit edit, CancellationToken ct) =>
        management.SaveLogosAsync(key, edit, ct);

    [HttpPost("{key}/draft/adopt-authority-base")]
    public Task<TemplateVersionDetail> AdoptAuthorityBase(string key, [FromQuery] uint revision, CancellationToken ct) =>
        management.AdoptAuthorityBaseAsync(key, revision, ct);

    [HttpDelete("{key}/draft")]
    public async Task<IActionResult> Discard(string key, [FromQuery] uint revision, CancellationToken ct)
    {
        await management.DiscardAsync(key, revision, ct);
        return NoContent();
    }

    [HttpPost("{key}/draft/preview")]
    public async Task<IActionResult> Preview(string key, [FromQuery] uint revision, [FromQuery] bool longSample, CancellationToken ct) =>
        File(await management.PreviewAsync(key, revision, longSample, ct), "application/pdf", $"{key}-prueba.pdf");

    [HttpPost("{key}/draft/publish")]
    public async Task<IActionResult> Publish(string key, TemplateActivationRequest request, CancellationToken ct)
    {
        await management.ActivateAsync(key, null, request, ct);
        return NoContent();
    }

    [HttpPost("{key}/versions/{id:long}/restore")]
    public async Task<IActionResult> Restore(string key, long id, TemplateActivationRequest request, CancellationToken ct)
    {
        await management.ActivateAsync(key, id, request, ct);
        return NoContent();
    }
}

internal sealed class TemplateAntiforgeryAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (HttpMethods.IsGet(context.HttpContext.Request.Method)) return;
        try
        {
            var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            context.Result = new BadRequestObjectResult(new ProblemDetails
            {
                Status = 400, Title = "Validación de solicitud", Detail = "Renueva la sesión de edición y vuelve a intentar."
            });
        }
    }
}
