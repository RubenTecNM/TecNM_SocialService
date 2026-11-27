using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TecNM.Api.Core.Exceptions;
using TecNM.Api.Data;
using TecNM.Api.Modules.Expedients.Pdf;

namespace TecNM.Api.Modules.Expedients.Templates;

public sealed class TemplateCatalog(TecNMDbContext db, OfficialPdfService pdf, TimeProvider clock, IConfiguration configuration)
{
    internal static readonly string[] SocialKeys =
    [
        "ss-application", "ss-commitment", "ss-report", "ss-supervisor-evaluation", "ss-self-evaluation",
        "ss-termination", "ss-activity", "ss-acceptance-example", "ss-individual-act"
    ];
    internal const string InternshipKey = "pp-evaluation";
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    internal static ConflictException Unavailable() => new("documents.template_unavailable", "La versión asignada de la plantilla no está disponible.");

    internal static OfficialPdfService.TemplateDefinition Layout(DocumentTemplateVersion version)
    {
        try
        {
            if (version.LayoutSchemaVersion == 1 && JsonSerializer.Deserialize<OfficialPdfService.TemplateDefinition>(version.Layout, Json) is { } layout)
                return layout;
        }
        catch (JsonException) { }
        throw Unavailable();
    }

    internal static void VerifyHash(DocumentTemplateVersion version)
    {
        if (!Hash(version.Pdf).Equals(version.Sha256, StringComparison.OrdinalIgnoreCase)) throw Unavailable();
    }

    public async Task<byte[]> ResolveAsync(string key, long? expedienteId, IReadOnlyDictionary<string, object?>? data, CancellationToken ct)
    {
        var versions = db.Set<DocumentTemplateVersion>().AsNoTracking();
        IQueryable<long?> selection = expedienteId is null
            ? db.Set<DocumentTemplate>().Where(t => t.Key == key).Select(t => t.ActiveVersionId)
            : db.Set<DocumentTemplateAssignment>().Where(a => a.TemplateKey == key &&
                (key == InternshipKey ? a.InternshipId == expedienteId : a.SocialServiceId == expedienteId)).Select(a => (long?)a.VersionId);
        var version = await versions.SingleOrDefaultAsync(v => v.TemplateKey == key && v.PublishedAt != null && selection.Contains(v.Id), ct)
            ?? throw Unavailable();
        VerifyHash(version);
        var layout = Layout(version);
        TemplatePdfValidation.ValidateGeometry(version.Pdf, layout);
        return data is null ? version.Pdf : pdf.Generate(layout, version.Pdf, data);
    }

    public async Task AssignAsync(string area, long id, CancellationToken ct)
    {
        var active = await db.Set<DocumentTemplate>().AsNoTracking().Where(t => t.Area == area && t.ActiveVersionId != null)
            .Select(t => new { t.Key, VersionId = t.ActiveVersionId!.Value }).ToListAsync(ct);
        var keys = area == "internship" ? [InternshipKey] : SocialKeys;
        if (!keys.ToHashSet().SetEquals(active.Select(t => t.Key))) throw Unavailable();
        foreach (var entry in active)
            db.Add(Assignment(entry.Key, entry.VersionId, area, id));
    }

    public async Task ImportAsync(CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var templates = db.Set<DocumentTemplate>();
        var existing = await templates.ToDictionaryAsync(t => t.Key, ct);
        var now = clock.GetUtcNow();
        var baselineRoot = configuration["Documents:BaselineRootPath"];
        foreach (var key in SocialKeys.Append(InternshipKey))
        {
            if (existing.ContainsKey(key)) continue;
            var current = CatalogVersion(key, pdf.Load(key), now);
            var initial = string.IsNullOrWhiteSpace(baselineRoot) ? current :
                CatalogVersion(key, OfficialPdfService.LoadFrom(baselineRoot, key), now);
            var template = new DocumentTemplate
            {
                Key = key, Area = key == InternshipKey ? "internship" : "social_service",
                Mode = key == "ss-acceptance-example" ? "static" : "prefill"
            };
            templates.Add(template);
            await db.SaveChangesAsync(ct);
            await ActivateInitialAsync(template, initial, "import", "Catálogo inicial", now, ct);
            if (current.Sha256 != initial.Sha256 || current.Layout != initial.Layout)
            {
                await ActivateInitialAsync(template, current, "publish", "Catálogo actualizado de la instalación", now, ct);
            }
        }

        var baselines = await db.Set<DocumentTemplateActivation>().AsNoTracking().Where(a => a.Action == "import")
            .Select(a => new { a.TemplateKey, a.VersionId }).ToListAsync(ct);
        foreach (var baseline in baselines.Where(item => SocialKeys.Contains(item.TemplateKey) || item.TemplateKey == InternshipKey))
        {
            var assigned = db.Set<DocumentTemplateAssignment>().Where(a => a.TemplateKey == baseline.TemplateKey);
            var internship = baseline.TemplateKey == InternshipKey;
            var unassigned = internship
                ? db.Internships.Where(t => t.DeletedAt == null && !assigned.Any(a => a.InternshipId == t.Id)).Select(t => t.Id)
                : db.SocialServices.Where(t => t.DeletedAt == null && !assigned.Any(a => a.SocialServiceId == t.Id)).Select(t => t.Id);
            foreach (var id in await unassigned.ToListAsync(ct))
                db.Add(Assignment(baseline.TemplateKey, baseline.VersionId, internship ? "internship" : "social_service", id));
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }

    private static DocumentTemplateVersion CatalogVersion(string key, (OfficialPdfService.TemplateDefinition Layout, byte[] Bytes) resource, DateTimeOffset now)
    {
        var (layout, bytes) = resource;
        TemplatePdfValidation.Inspect(bytes);
        if (layout.Institutional || layout.Stamps is { Length: > 0 } ||
            (layout.Fields.Length == 0) != (key == "ss-acceptance-example")) throw Unavailable();
        return new DocumentTemplateVersion
        {
            TemplateKey = key, Pdf = bytes, Sha256 = Hash(bytes), Layout = JsonSerializer.Serialize(layout, Json),
            CreatedAt = now, UpdatedAt = now, PublishedAt = now
        };
    }

    private async Task ActivateInitialAsync(DocumentTemplate template, DocumentTemplateVersion version, string action,
        string reason, DateTimeOffset now, CancellationToken ct)
    {
        db.Add(version);
        await db.SaveChangesAsync(ct);
        db.Add(new DocumentTemplateActivation
        {
            TemplateKey = template.Key, PreviousVersionId = template.ActiveVersionId,
            VersionId = version.Id, Action = action, Reason = reason, CreatedAt = now
        });
        template.ActiveVersionId = version.Id;
        await db.SaveChangesAsync(ct);
    }

    private static DocumentTemplateAssignment Assignment(string key, long version, string area, long id) =>
        new() { TemplateKey = key, VersionId = version, InternshipId = area == "internship" ? id : null, SocialServiceId = area == "social_service" ? id : null };
}
