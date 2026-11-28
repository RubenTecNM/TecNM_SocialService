using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TecNM.Api.Modules.Expedients.Pdf;

namespace TecNM.Api.Modules.Expedients.Templates;

internal sealed class DocumentTemplate
{
    public string Key { get; set; } = "";
    public string Area { get; set; } = "";
    public string Mode { get; set; } = "";
    public long? ActiveVersionId { get; set; }
    public uint Revision { get; set; }

    internal static void Configure(ModelBuilder model)
    {
        Map<DocumentTemplate>("document_templates");
        Map<DocumentTemplateVersion>("document_template_versions");
        Map<DocumentTemplateAssignment>("document_template_assignments");
        Map<DocumentTemplateActivation>("document_template_activations");
        Map<DocumentLogo>("document_logos");
        Map<DocumentTemplateLogo>("document_template_logos");
        model.Entity<DocumentTemplate>().HasKey(item => item.Key);
        model.Entity<DocumentTemplate>().Property(item => item.Revision).HasColumnName("xmin").IsRowVersion();
        model.Entity<DocumentTemplateVersion>().Property(item => item.Revision).HasColumnName("xmin").IsRowVersion();
        model.Entity<DocumentTemplateVersion>().Property(item => item.Layout).HasColumnType("jsonb");

        void Map<T>(string table) where T : class
        {
            var entity = model.Entity<T>().ToTable(table);
            foreach (var property in entity.Metadata.GetProperties())
                property.SetColumnName(Regex.Replace(property.Name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant());
        }
    }
}

internal sealed class DocumentTemplateVersion
{
    public long Id { get; set; }
    public string TemplateKey { get; set; } = "";
    public long? SourceVersionId { get; set; }
    public long? AuthorityCompatibilityForId { get; set; }
    public byte[] Pdf { get; set; } = [];
    public string Sha256 { get; set; } = "";
    public string Layout { get; set; } = "";
    public int LayoutSchemaVersion { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public uint Revision { get; set; }
}

internal sealed class DocumentTemplateAssignment
{
    public long Id { get; set; }
    public string TemplateKey { get; set; } = "";
    public long VersionId { get; set; }
    public long? SocialServiceId { get; set; }
    public long? InternshipId { get; set; }
}

internal sealed class DocumentTemplateActivation
{
    public long Id { get; set; }
    public string TemplateKey { get; set; } = "";
    public long? PreviousVersionId { get; set; }
    public long VersionId { get; set; }
    public string Action { get; set; } = "";
    public long? ActorId { get; set; }
    public string Reason { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed record TemplateSummary(string Key, string Area, string Mode, long? ActiveVersionId, uint Revision, long? DraftId);
public sealed record TemplateVersionSummary(long Id, DateTimeOffset CreatedAt, DateTimeOffset? PublishedAt, string Sha256);
public sealed record TemplateActivationSummary(long? PreviousVersionId, long VersionId, string Action, long? ActorId, string Reason, DateTimeOffset CreatedAt);
public sealed record TemplateVersionDetail(long Id, uint Revision, DateTimeOffset? PublishedAt,
    OfficialPdfService.TemplateDefinition Layout, OfficialPdfService.Field[] Slots,
    TemplateLogoPlacement[] Logos, bool CanAdoptAuthorityBase);
public sealed record TemplatePlacement(double X, double Y, double Width, double Height, int Page,
    string Alignment, double? FontSize, double? MinFontSize, OfficialPdfService.TextRow[]? Rows);
public sealed record TemplateLayoutEdit(uint Revision, string Font, double FontSize, double MinFontSize, TemplatePlacement?[] Fields);
public sealed record TemplateActivationRequest(uint TemplateRevision, uint? DraftRevision, string Reason);

internal sealed class DocumentLogo
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public byte[] Png { get; set; } = [];
    public int Width { get; set; }
    public int Height { get; set; }
    public string Sha256 { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
}

internal sealed class DocumentTemplateLogo
{
    public long Id { get; set; }
    public long VersionId { get; set; }
    public int Slot { get; set; }
    public long LogoId { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

public sealed record TemplateLogoPlacement(int Slot, long LogoId, double X, double Y, double Width, double Height);
public sealed record TemplateLogoEdit(uint Revision, TemplateLogoPlacement[] Logos);
public sealed record LogoSummary(long Id, string Name, int Width, int Height, string Sha256, DateTimeOffset CreatedAt);
