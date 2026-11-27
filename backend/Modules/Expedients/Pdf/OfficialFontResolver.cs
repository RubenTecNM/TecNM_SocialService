using PdfSharp.Fonts;
using TecNM.Api.Core.Exceptions;

namespace TecNM.Api.Modules.Expedients.Pdf;

public sealed class OfficialFontResolver(string root) : IFontResolver
{
    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic) =>
        familyName.ToUpperInvariant() switch
        {
            "ARIAL" => new("arial.ttf"),
            "CENTURY GOTHIC" => new("GOTHIC.TTF"),
            _ => throw FontUnavailable()
        };

    public byte[] GetFont(string faceName)
    {
        var path = Path.Combine(root, faceName);
        if (!File.Exists(path)) throw FontUnavailable();
        return File.ReadAllBytes(path);
    }

    private static ConflictException FontUnavailable() => new("documents.font_unavailable",
        "Configura las fuentes autorizadas para generar este formato.");
}
