using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using TecNM.Api.Core.Exceptions;
using TecNM.Api.Modules.Expedients.Pdf;

namespace TecNM.Api.Modules.Expedients.Templates;

internal static class TemplatePdfValidation
{
    internal const int MaxBytes = 5 * 1024 * 1024;

    private static readonly string[] ActiveKeys =
        ["/AcroForm", "/Annots", "/AA", "/OpenAction", "/JavaScript", "/JS", "/EmbeddedFiles", "/EF", "/AF", "/XFA", "/Perms"];
    private static readonly HashSet<string> Actions =
        ["/GoTo", "/GoToR", "/GoToE", "/Launch", "/Thread", "/URI", "/Sound", "/Movie", "/Hide", "/Named",
         "/SubmitForm", "/ResetForm", "/ImportData", "/JavaScript", "/SetOCGState", "/Rendition", "/Trans", "/GoTo3DView", "/RichMediaExecute"];

    internal static OfficialPdfService.PageSize[] Inspect(byte[] bytes)
    {
        if (bytes.Length > MaxBytes) throw new AppException(413, "files.too_large", "La plantilla supera los 5 MiB.");
        if (!bytes.AsSpan().StartsWith("%PDF-"u8)) throw Invalid();
        try
        {
            using var source = new MemoryStream(bytes);
            using var pdf = PdfReader.Open(source, PdfDocumentOpenMode.Import);
            if (pdf.SecuritySettings.IsEncrypted || pdf.PageCount == 0) throw Invalid();
            var pending = new Stack<PdfItem>(pdf.Internals.GetAllObjects());
            var seen = new HashSet<PdfItem>(ReferenceEqualityComparer.Instance);
            while (pending.TryPop(out var item))
            {
                if (!seen.Add(item)) continue;
                if (item is PdfReference { Value: { } value }) pending.Push(value);
                else if (item is PdfArray array)
                    foreach (var child in array.Elements) pending.Push(child);
                else if (item is PdfDictionary dict)
                {
                    if (ActiveKeys.Any(dict.Elements.ContainsKey) || Actions.Contains(dict.Elements.GetName("/S")) ||
                        dict.Elements.GetName("/Type") is "/Action" or "/Filespec" or "/EmbeddedFile" or "/Sig") throw Invalid();
                    foreach (var child in dict.Elements.Values)
                        if (child is not null) pending.Push(child);
                }
            }

            var sizes = new List<OfficialPdfService.PageSize>(pdf.PageCount);
            foreach (PdfPage page in pdf.Pages)
            {
                var media = page.MediaBox;
                var crop = page.HasCropBox ? page.CropBox : media;
                if (page.Rotate != 0 || media.X1 != 0 || media.Y1 != 0 || crop.X1 != 0 || crop.Y1 != 0 ||
                    crop.Width != media.Width || crop.Height != media.Height ||
                    (page.Elements.ContainsKey("/UserUnit") && page.Elements.GetReal("/UserUnit") != 1) ||
                    !double.IsFinite(media.Width) || !double.IsFinite(media.Height) ||
                    media.Width <= 0 || media.Height <= 0) throw Invalid();
                sizes.Add(new OfficialPdfService.PageSize(media.Width, media.Height));
            }
            return sizes.ToArray();
        }
        catch (Exception ex) when (PdfError(ex)) { throw Invalid(); }
    }

    internal static void ValidateGeometry(byte[] bytes, OfficialPdfService.TemplateDefinition layout)
    {
        try
        {
            using var source = new MemoryStream(bytes);
            using var pdf = PdfReader.Open(source, PdfDocumentOpenMode.Import);
            OfficialPdfService.ValidateLayout(layout, pdf);
        }
        catch (Exception ex) when (PdfError(ex)) { throw TemplateCatalog.Unavailable(); }
    }

    internal static void ValidateEditableGeometry(byte[] bytes, OfficialPdfService.TemplateDefinition layout)
    {
        using var source = new MemoryStream(bytes);
        using var pdf = PdfReader.Open(source, PdfDocumentOpenMode.Import);
        var errors = new Dictionary<string, string[]>();
        try { OfficialPdfService.ValidateLayout(layout with { Fields = [] }, pdf); }
        catch (ConflictException)
        {
            throw new BusinessRuleException("templates.invalid_layout", "Revisa los tamaños nominal y mínimo de la plantilla.");
        }
        foreach (var field in layout.Fields.Where(field => field is not null))
        {
            try { OfficialPdfService.ValidateLayout(layout with { Fields = [field] }, pdf); }
            catch (ConflictException)
            {
                errors[field.Key] = ["Revisa la caja, los renglones y el tamaño de este campo."];
            }
        }
        if (errors.Count > 0)
            throw new BusinessRuleException("templates.invalid_layout", "Revisa la geometría de los campos.", errors);
    }

    private static bool PdfError(Exception ex) =>
        ex is PdfReaderException or ArgumentException or InvalidOperationException or IOException or FormatException;

    private static AppException Invalid() => new(415, "templates.invalid_pdf",
        "Carga un PDF estático limpio, sin contraseña, formularios, anotaciones, acciones ni adjuntos, con páginas sin rotación ni recorte.");
}
