using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TecNM.Api.Data;
using TecNM.Api.Modules.Expedients.Pdf;
using static TecNM.Api.Modules.Expedients.Templates.TemplateCatalog;

namespace TecNM.Api.Modules.Expedients.Templates;

public sealed class BrandingImport(TecNMDbContext db, IWebHostEnvironment environment, TimeProvider clock)
{
    private const string ActivationReason = "Activación de identidad documental administrable";
    private sealed record AssetZone(string Name, int Page, double X, double Y, double Width, double Height, string Png);
    private sealed record Resource(string Sha256, string File, string? AuthorityFile, OfficialPdfService.AuthorityField[] AuthorityFields, AssetZone[] Zones);

    public async Task RunAsync(CancellationToken ct = default)
    {
        var root = Path.Combine(environment.ContentRootPath, "Resources", "Documents", "branding");
        var resources = JsonSerializer.Deserialize<Dictionary<string, Resource>>(await File.ReadAllTextAsync(Path.Combine(root, "manifest.json"), ct), Json)
            ?? throw Unavailable();
        var versions = db.Set<DocumentTemplateVersion>();
        var now = clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var initialized = await db.Set<DocumentTemplateActivation>().AsNoTracking()
            .Where(item => item.Action == "publish" && item.ActorId == null && item.Reason == ActivationReason)
            .Select(item => item.TemplateKey).ToListAsync(ct);
        var logos = await db.Set<DocumentLogo>().AsNoTracking().Select(logo => new { logo.Sha256, logo.Id }).ToListAsync(ct);
        var byHash = logos.GroupBy(logo => logo.Sha256).ToDictionary(group => group.Key, group => group.First().Id);
        foreach (var (key, resource) in resources)
        {
            if (initialized.Contains(key)) continue;
            var template = await db.Set<DocumentTemplate>().SingleAsync(template => template.Key == key, ct);
            var initial = await versions.SingleAsync(version => version.Id == template.ActiveVersionId && version.TemplateKey == key, ct);
            if (!initial.Sha256.Equals(resource.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                if (Layout(initial).LogoZones is { Length: > 0 } || Layout(initial).AuthorityFields is { Length: > 0 }) continue;
                throw new InvalidOperationException($"La plantilla activa {key} no coincide con la fuente calibrada.");
            }

            if (resource.AuthorityFile is not null)
            {
                var originals = await versions.AsNoTracking()
                    .Where(v => v.TemplateKey == key && v.PublishedAt != null).ToListAsync(ct);
                var entries = originals.Where(v => Layout(v).AuthorityFields is not { Length: > 0 }).ToList();
                foreach (var original in entries)
                {
                    if (!original.Sha256.Equals(resource.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        if (original.Id == initial.Id || await db.Set<DocumentTemplateAssignment>().AnyAsync(a => a.VersionId == original.Id, ct))
                            throw new InvalidOperationException($"La versión histórica {original.Id} de {key} requiere una base compatible propia.");
                        continue;
                    }
                    if (!originals.Any(version => version.AuthorityCompatibilityForId == original.Id))
                        await AddVersion(original, resource.AuthorityFile, [], original.Id);
                }
            }

            DocumentTemplateVersion next;
            if (resource.Zones.Length == 0)
                next = await versions.SingleAsync(v => v.AuthorityCompatibilityForId == initial.Id, ct);
            else
            {
                next = await AddVersion(initial, resource.File, resource.Zones, null);
                for (var i = 0; i < resource.Zones.Length; i++)
                {
                    var zone = resource.Zones[i];
                    var bytes = await File.ReadAllBytesAsync(Path.Combine(root, zone.Png), ct);
                    var hash = Hash(bytes);
                    if (!byHash.TryGetValue(hash, out var logoId))
                    {
                        var asset = TemplateBranding.NewLogo(zone.Name, bytes, now);
                        db.Add(asset);
                        await db.SaveChangesAsync(ct);
                        logoId = asset.Id;
                        byHash.Add(hash, logoId);
                    }
                    db.Add(TemplateBranding.Attach(next.Id, new(i, logoId, zone.X, zone.Y, zone.Width, zone.Height)));
                }
            }
            db.Add(new DocumentTemplateActivation
            {
                TemplateKey = key, PreviousVersionId = initial.Id, VersionId = next.Id, Action = "publish",
                Reason = ActivationReason, CreatedAt = now
            });
            template.ActiveVersionId = next.Id;
            await db.SaveChangesAsync(ct);

            async Task<DocumentTemplateVersion> AddVersion(DocumentTemplateVersion original, string name, AssetZone[] zones, long? compatibleFor)
            {
                VerifyHash(original);
                var bytes = await File.ReadAllBytesAsync(Path.Combine(root, name), ct);
                TemplatePdfValidation.Inspect(bytes);
                var hash = Hash(bytes);
                var layout = Layout(original) with
                {
                    Sha256 = hash, Version = Layout(original).Version + "-identidad",
                    AuthorityFields = resource.AuthorityFields,
                    LogoZones = zones.Select(z => new OfficialPdfService.LogoZone(z.Name, z.Page, z.X, z.Y, z.Width, z.Height)).ToArray()
                };
                TemplatePdfValidation.ValidateGeometry(bytes,
                    layout with { Fields = layout.Fields.Concat(resource.AuthorityFields.Select(field => field.Placement)).ToArray() });
                var version = new DocumentTemplateVersion
                {
                    TemplateKey = key, SourceVersionId = original.Id, AuthorityCompatibilityForId = compatibleFor,
                    Pdf = bytes, Sha256 = hash, Layout = JsonSerializer.Serialize(layout, Json),
                    PublishedAt = now, CreatedAt = now, UpdatedAt = now
                };
                db.Add(version);
                await db.SaveChangesAsync(ct);
                return version;
            }
        }
        await transaction.CommitAsync(ct);
    }
}
