using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TecNM.Api.Core.Exceptions;
using TecNM.Api.Core.Security;
using TecNM.Api.Data;
using TecNM.Api.Modules.Expedients.Pdf;
using static TecNM.Api.Modules.Expedients.Templates.TemplateCatalog;

namespace TecNM.Api.Modules.Expedients.Templates;

public sealed class TemplateManagement(TecNMDbContext db, OfficialPdfService pdf, ICurrentUser user, TimeProvider clock)
{
    private DbSet<DocumentTemplate> Templates => db.Set<DocumentTemplate>();
    private DbSet<DocumentTemplateVersion> Versions => db.Set<DocumentTemplateVersion>();

    public async Task<List<TemplateSummary>> ListAsync(CancellationToken ct)
    {
        var areas = new[] { "social_service", "internship" }.Where(CanManage).ToArray();
        return await Templates.AsNoTracking().Where(t => areas.Contains(t.Area)).OrderBy(t => t.Area).ThenBy(t => t.Key)
            .Select(t => new TemplateSummary(t.Key, t.Area, t.Mode, t.ActiveVersionId, t.Revision,
                Versions.Where(v => v.TemplateKey == t.Key && v.PublishedAt == null).Select(v => (long?)v.Id).SingleOrDefault()))
            .ToListAsync(ct);
    }

    public async Task<List<TemplateVersionSummary>> HistoryAsync(string key, CancellationToken ct)
    {
        await GetTemplate(key, ct);
        return await Versions.AsNoTracking().Where(v => v.TemplateKey == key).OrderByDescending(v => v.Id)
            .Select(v => new TemplateVersionSummary(v.Id, v.CreatedAt, v.PublishedAt, v.Sha256)).ToListAsync(ct);
    }

    public async Task<List<TemplateActivationSummary>> ActivationsAsync(string key, CancellationToken ct)
    {
        await GetTemplate(key, ct);
        return await db.Set<DocumentTemplateActivation>().AsNoTracking().Where(a => a.TemplateKey == key).OrderByDescending(a => a.Id)
            .Select(a => new TemplateActivationSummary(a.PreviousVersionId, a.VersionId, a.Action, a.ActorId, a.Reason, a.CreatedAt)).ToListAsync(ct);
    }

    public async Task<TemplateVersionDetail> DetailAsync(string key, long id, CancellationToken ct)
    {
        await GetTemplate(key, ct);
        return await Detail(await GetVersion(key, id, ct), ct);
    }

    public async Task<byte[]> FileAsync(string key, long id, CancellationToken ct)
    {
        await GetTemplate(key, ct);
        var version = await GetVersion(key, id, ct);
        VerifyHash(version);
        return version.Pdf;
    }

    public async Task<TemplateVersionDetail> CreateDraftAsync(string key, CancellationToken ct)
    {
        var template = await GetTemplate(key, ct);
        if (await Versions.AnyAsync(v => v.TemplateKey == key && v.PublishedAt == null, ct)) throw Changed();
        var active = await GetVersion(key, template.ActiveVersionId ?? throw Unavailable(), ct);
        VerifyHash(active);
        var draft = new DocumentTemplateVersion
        {
            TemplateKey = key, SourceVersionId = active.Id, Pdf = active.Pdf, Layout = active.Layout, Sha256 = active.Sha256,
            CreatedAt = clock.GetUtcNow(), CreatedBy = user.Id
        };
        Touch(draft);
        db.Add(draft);
        await Save(ct);
        return await Detail(draft, ct);
    }

    public async Task<TemplateVersionDetail> ReplaceFileAsync(string key, uint revision, IFormFile file, CancellationToken ct)
    {
        var draft = await GetDraft(key, revision, ct);
        if (file.Length > TemplatePdfValidation.MaxBytes) throw new AppException(413, "files.too_large", "La plantilla supera los 5 MiB.");
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var sizes = TemplatePdfValidation.Inspect(bytes);
        var layout = Layout(draft);
        var fields = layout.Fields.Select(field => field is not null && field.Page >= 0 && field.Page < sizes.Length &&
            field.X + field.Width <= sizes[field.Page].Width && field.Y + field.Height <= sizes[field.Page].Height ? field : null!);
        draft.Pdf = bytes;
        draft.Sha256 = Hash(bytes);
        draft.Layout = JsonSerializer.Serialize(layout with
        {
            Sha256 = draft.Sha256, Pages = sizes.Length, PageSizes = sizes, Fields = fields.ToArray()
        }, Json);
        Touch(draft);
        await Save(ct);
        return await Detail(draft, ct);
    }

    public async Task<TemplateVersionDetail> SaveLayoutAsync(string key, TemplateLayoutEdit edit, CancellationToken ct)
    {
        var draft = await GetDraft(key, edit.Revision, ct);
        var slots = await Slots(draft, ct);
        if (edit.Font is not ("Arial" or "Century Gothic") || edit.Fields is null || edit.Fields.Length != slots.Length)
            throw InvalidLayout();
        var fields = edit.Fields.Select((item, index) => item is null ? null! : slots[index] with
        {
            X = item.X, Y = item.Y, Width = item.Width, Height = item.Height, Page = item.Page,
            Alignment = item.Alignment, FontSize = item.FontSize, MinFontSize = item.MinFontSize, Rows = item.Rows
        }).ToArray();
        var layout = Layout(draft) with { Font = edit.Font, FontSize = edit.FontSize, MinFontSize = edit.MinFontSize, Fields = fields };
        VerifyHash(draft);
        TemplatePdfValidation.ValidateEditableGeometry(draft.Pdf, layout);
        draft.Layout = JsonSerializer.Serialize(layout, Json);
        Touch(draft);
        await Save(ct);
        return await Detail(draft, ct);
    }

    public async Task DiscardAsync(string key, uint revision, CancellationToken ct)
    {
        db.Remove(await GetDraft(key, revision, ct));
        await Save(ct);
    }

    public async Task<byte[]> PreviewAsync(string key, uint revision, bool longSample, CancellationToken ct) =>
        Render(await GetDraft(key, revision, ct), longSample);

    public async Task ActivateAsync(string key, long? restoreId, TemplateActivationRequest request, CancellationToken ct)
    {
        var template = await GetTemplate(key, ct);
        if (template.Revision != request.TemplateRevision) throw Changed();
        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.Length > 1000)
            throw new BusinessRuleException("templates.reason_required", "Escribe un motivo de hasta 1000 caracteres.");
        var version = restoreId is long id ? await GetVersion(key, id, ct) :
            await GetDraft(key, request.DraftRevision ?? throw Changed(), ct);
        if (restoreId is not null && version.PublishedAt is null) throw Changed();
        if (template.ActiveVersionId == version.Id) return;
        TemplatePdfValidation.Inspect(version.Pdf);
        Render(version, longSample: false);
        Render(version, longSample: true);

        var now = clock.GetUtcNow();
        var previous = template.ActiveVersionId;
        if (restoreId is null)
        {
            version.PublishedAt = now;
            Touch(version);
        }
        template.ActiveVersionId = version.Id;
        db.Add(new DocumentTemplateActivation
        {
            TemplateKey = key, PreviousVersionId = previous, VersionId = version.Id, ActorId = user.Id,
            Action = restoreId is null ? "publish" : "restore", Reason = reason, CreatedAt = now
        });
        await Save(ct);
    }

    private byte[] Render(DocumentTemplateVersion version, bool longSample)
    {
        VerifyHash(version);
        var layout = Layout(version);
        if (layout.Fields.Any(f => f is null))
            throw new BusinessRuleException("templates.fields_unplaced", "Ubica todos los campos antes de previsualizar.");
        TemplatePdfValidation.ValidateGeometry(version.Pdf, layout);
        return layout.Fields.Length == 0 ? version.Pdf :
            pdf.Generate(layout, version.Pdf, TemplateSamples.Create(longSample));
    }

    private async Task<DocumentTemplate> GetTemplate(string key, CancellationToken ct)
    {
        var template = await Templates.SingleOrDefaultAsync(t => t.Key == key, ct)
            ?? throw new NotFoundException("documents.template_not_found", "La plantilla no existe.");
        if (!CanManage(template.Area)) throw new AppException(403, "templates.forbidden", "La plantilla pertenece a otra área.");
        return template;
    }

    private bool CanManage(string area) => user.IsInRole("admin") ||
        user.IsInRole(area == "internship" ? "internship_coordinator" : "social_service_officer");

    private async Task<DocumentTemplateVersion> GetDraft(string key, uint revision, CancellationToken ct)
    {
        await GetTemplate(key, ct);
        var draft = await Versions.SingleOrDefaultAsync(v => v.TemplateKey == key && v.PublishedAt == null, ct);
        return draft?.Revision == revision ? draft : throw Changed();
    }

    private async Task<DocumentTemplateVersion> GetVersion(string key, long id, CancellationToken ct) =>
        await Versions.SingleOrDefaultAsync(v => v.TemplateKey == key && v.Id == id, ct)
        ?? throw new NotFoundException("templates.version_not_found", "La versión no existe.");

    private async Task<OfficialPdfService.Field[]> Slots(DocumentTemplateVersion version, CancellationToken ct)
    {
        if (version.PublishedAt is not null) return Layout(version).Fields;
        var source = await Versions.AsNoTracking()
            .Where(v => v.TemplateKey == version.TemplateKey && v.Id == version.SourceVersionId && v.PublishedAt != null)
            .Select(v => new DocumentTemplateVersion { Layout = v.Layout, LayoutSchemaVersion = v.LayoutSchemaVersion })
            .SingleOrDefaultAsync(ct) ?? throw Unavailable();
        return Layout(source).Fields;
    }

    private async Task<TemplateVersionDetail> Detail(DocumentTemplateVersion version, CancellationToken ct) =>
        new(version.Id, version.Revision, version.PublishedAt, Layout(version), await Slots(version, ct));

    private void Touch(DocumentTemplateVersion draft)
    {
        draft.UpdatedAt = clock.GetUtcNow();
        draft.UpdatedBy = user.Id;
    }

    private async Task Save(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw Changed(); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException
            { ConstraintName: "uq_document_template_versions_draft" }) { throw Changed(); }
    }

    private static ConflictException Changed() => new("templates.concurrency", "La plantilla cambió. Vuelve a cargar su estado.");
    private static BusinessRuleException InvalidLayout() => new("templates.invalid_layout", "Revisa la fuente y la geometría de los campos.");
}
