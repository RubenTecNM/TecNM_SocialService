using System.Buffers.Binary;
using Microsoft.EntityFrameworkCore;
using PdfSharp.Drawing;
using TecNM.Api.Core.Exceptions;
using TecNM.Api.Core.Security;
using TecNM.Api.Data;
using TecNM.Api.Modules.Expedients.Pdf;
using static TecNM.Api.Modules.Expedients.Templates.TemplateCatalog;

namespace TecNM.Api.Modules.Expedients.Templates;

public sealed class TemplateBranding(TecNMDbContext db, OfficialPdfService pdf, ICurrentUser user, TimeProvider clock)
{
    internal static bool RequiresAuthorities(string key) => key is "ss-termination" or "ss-acceptance-example";

    public Task<List<LogoSummary>> ListAsync(CancellationToken ct) =>
        db.Set<DocumentLogo>().AsNoTracking().OrderByDescending(logo => logo.Id)
            .Select(logo => new LogoSummary(logo.Id, logo.Name, logo.Width, logo.Height, logo.Sha256, logo.CreatedAt)).ToListAsync(ct);

    public async Task<byte[]> ImageAsync(long id, CancellationToken ct)
    {
        var logo = await db.Set<DocumentLogo>().AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new NotFoundException("templates.logo_not_found", "El logotipo no existe.");
        if (Hash(logo.Png) != logo.Sha256) throw Unavailable();
        return logo.Png;
    }

    public async Task<LogoSummary> UploadAsync(string name, IFormFile file, CancellationToken ct)
    {
        if (file.Length > 2 * 1024 * 1024) throw new AppException(413, "files.too_large", "El logotipo supera los 2 MiB.");
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        var logo = NewLogo(name, buffer.ToArray(), clock.GetUtcNow(), user.Id);
        db.Add(logo);
        await db.SaveChangesAsync(ct);
        return new(logo.Id, logo.Name, logo.Width, logo.Height, logo.Sha256, logo.CreatedAt);
    }

    internal static DocumentLogo NewLogo(string name, byte[] bytes, DateTimeOffset now, long? actor = null)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 100)
            throw new BusinessRuleException("templates.invalid_logo_name", "Escribe un nombre de hasta 100 caracteres.");
        if (bytes.Length is < 33 or > 2 * 1024 * 1024 ||
            !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
            !bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8)) throw InvalidPng();
        var width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));
        if (width is < 1 or > 4096 || height is < 1 or > 4096) throw InvalidPng();
        try
        {
            using var stream = new MemoryStream(bytes);
            using var image = XImage.FromStream(stream);
            if (image.PixelWidth != width || image.PixelHeight != height) throw InvalidPng();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or FormatException or NotSupportedException)
        { throw InvalidPng(); }
        return new() { Name = name, Png = bytes, Width = width, Height = height, Sha256 = Hash(bytes), CreatedAt = now, CreatedBy = actor };
    }

    internal Task<TemplateLogoPlacement[]> PlacementsAsync(long versionId, CancellationToken ct) =>
        db.Set<DocumentTemplateLogo>().AsNoTracking().Where(logo => logo.VersionId == versionId).OrderBy(logo => logo.Slot)
            .Select(logo => new TemplateLogoPlacement(logo.Slot, logo.LogoId, logo.X, logo.Y, logo.Width, logo.Height)).ToArrayAsync(ct);

    internal static DocumentTemplateLogo Attach(long versionId, TemplateLogoPlacement placement) => new()
    {
        VersionId = versionId, Slot = placement.Slot, LogoId = placement.LogoId,
        X = placement.X, Y = placement.Y, Width = placement.Width, Height = placement.Height
    };

    internal async Task ValidateLogosAsync(OfficialPdfService.TemplateDefinition layout, TemplateLogoPlacement[] placements, CancellationToken ct)
    {
        var zones = layout.LogoZones ?? [];
        if (placements.Length != zones.Length || placements.Select(p => p.Slot).Distinct().Count() != zones.Length)
            throw InvalidPlacement();
        foreach (var placement in placements)
        {
            if (placement.Slot < 0 || placement.Slot >= zones.Length) throw InvalidPlacement();
            var zone = zones[placement.Slot];
            if (layout.PageSizes is null || zone.Page < 0 || zone.Page >= layout.PageSizes.Length ||
                !new[] { placement.X, placement.Y, placement.Width, placement.Height }.All(double.IsFinite) ||
                placement.Width <= 0 || placement.Height <= 0 || placement.X < zone.X || placement.Y < zone.Y ||
                placement.X + placement.Width > zone.X + zone.Width + .001 || placement.Y + placement.Height > zone.Y + zone.Height + .001 ||
                placement.X + placement.Width > layout.PageSizes[zone.Page].Width + .001 ||
                placement.Y + placement.Height > layout.PageSizes[zone.Page].Height + .001) throw InvalidPlacement();
        }
        if (placements.Length == 0) return;
        var ids = placements.Select(placement => placement.LogoId).Distinct().ToArray();
        if (await db.Set<DocumentLogo>().CountAsync(logo => ids.Contains(logo.Id), ct) != ids.Length) throw InvalidPlacement();
    }

    internal Task<DocumentTemplateVersion?> CompatibleAsync(DocumentTemplateVersion version, CancellationToken ct) =>
        db.Set<DocumentTemplateVersion>().AsNoTracking().SingleOrDefaultAsync(candidate =>
            candidate.TemplateKey == version.TemplateKey && candidate.AuthorityCompatibilityForId == version.Id && candidate.PublishedAt != null, ct);

    internal async Task<byte[]> RenderAsync(DocumentTemplateVersion version, IReadOnlyDictionary<string, object?>? data, CancellationToken ct)
    {
        VerifyHash(version);
        var layout = Layout(version);
        TemplatePdfValidation.ValidateGeometry(version.Pdf, layout);
        var fields = data is null ? [] : layout.Fields.ToList();
        var values = data is null ? new Dictionary<string, object?>() : new(data);
        if (layout.AuthorityFields is { Length: > 0 } authorities)
        {
            var roles = authorities.Select(item => item.Role).Distinct().ToArray();
            var active = await db.InstitutionalAuthorities.AsNoTracking()
                .Where(authority => authority.IsActive && roles.Contains(authority.RoleKey))
                .ToDictionaryAsync(authority => authority.RoleKey, ct);
            foreach (var binding in authorities)
            {
                var key = binding.Placement.Key;
                if (!active.TryGetValue(binding.Role, out var authority))
                    throw new BusinessRuleException("documents.data_missing", "Configura la autoridad institucional vigente.",
                        new Dictionary<string, string[]> { [key] = ["Falta un titular activo para este cargo."] });
                values[key] = binding.Part == "position" ? authority.Position :
                    binding.Prefix + string.Join(" ", new[] { authority.Title, authority.FullName }.Where(value => !string.IsNullOrWhiteSpace(value)));
                fields.Add(binding.Placement);
            }
        }
        List<OfficialPdfService.LogoImage> images = [];
        if (layout.LogoZones is { Length: > 0 } zones)
        {
            var placements = await PlacementsAsync(version.Id, ct);
            await ValidateLogosAsync(layout, placements, ct);
            var ids = placements.Select(item => item.LogoId).Distinct().ToArray();
            var assets = await db.Set<DocumentLogo>().AsNoTracking().Where(logo => ids.Contains(logo.Id)).ToDictionaryAsync(logo => logo.Id, ct);
            foreach (var placement in placements)
            {
                var asset = assets[placement.LogoId];
                if (Hash(asset.Png) != asset.Sha256) throw Unavailable();
                images.Add(new(asset.Png, zones[placement.Slot].Page, placement.X, placement.Y, placement.Width, placement.Height));
            }
        }
        return fields.Count == 0 && images.Count == 0 ? version.Pdf : pdf.Generate(layout with { Fields = fields.ToArray() }, version.Pdf, values, images);
    }

    private static AppException InvalidPng() => new(415, "templates.invalid_logo", "Usa un PNG válido de hasta 4096 píxeles por lado.");
    private static BusinessRuleException InvalidPlacement() =>
        new("templates.invalid_logo_placement", "Selecciona un logotipo por zona y colócalo dentro del rectángulo autorizado.");
}
