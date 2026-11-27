using PdfSharp.Pdf;
using System.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using PdfSharp.Drawing;
using PdfSharp.Pdf.IO;
using TecNM.Api.Core.Exceptions;

namespace TecNM.Api.Modules.Expedients.Pdf;

public sealed class OfficialPdfService(IWebHostEnvironment environment, IConfiguration configuration)
{
    private string Root => configuration["Documents:RootPath"] ?? Path.Combine(environment.ContentRootPath, "Resources", "Documents");
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public (byte[] Bytes, string FileName) Template(string key)
    {
        var (definition, bytes) = Load(key);
        return (bytes, key + ".pdf");
    }

    public string RequireInstitutionalTemplate(string key)
    {
        var (definition, _) = Load(key);
        if (!definition.Institutional || definition.Stamps is null
            || !definition.Stamps.Any(s => s.File.Replace('\\', '/').StartsWith("stamps/"))
            || !definition.Stamps.Any(s => s.File.Replace('\\', '/').StartsWith("signatures/"))) throw Unavailable();
        return key + ":" + definition.Version;
    }

    public byte[] Generate(string key, IReadOnlyDictionary<string, object?> data)
    {
        var (definition, bytes) = Load(key);
        if (definition.Fields.Length == 0) throw new ConflictException("documents.template_unavailable", "El formato aún no tiene un mapa de prellenado verificado.");
        using var source = new MemoryStream(bytes);
        using var pdf = PdfReader.Open(source, PdfDocumentOpenMode.Modify);
        foreach (var group in definition.Fields.GroupBy(f => f.Page))
        {
            using var graphics = XGraphics.FromPdfPage(pdf.Pages[group.Key], XGraphicsPdfPageOptions.Append);
            foreach (var field in group)
            {
                data.TryGetValue(field.Key, out var raw);
                var value = raw switch
                {
                    DateOnly date => date.ToString(field.Format ?? "dd/MM/yyyy", CultureInfo.GetCultureInfo("es-MX")),
                    DateTimeOffset date => date.ToString("dd/MM/yyyy"),
                    null => "",
                    _ => Convert.ToString(raw, CultureInfo.GetCultureInfo("es-MX")) ?? ""
                };
                if (field.MatchValue is not null)
                {
                    if (!definition.Fields.Where(f => f.Key == field.Key && f.MatchValue != null).Any(f => string.Equals(f.MatchValue, value, StringComparison.OrdinalIgnoreCase)))
                        throw new BusinessRuleException("documents.data_missing", $"El campo {field.Key} no tiene una casilla oficial configurada.");
                    if (!string.Equals(value, field.MatchValue, StringComparison.OrdinalIgnoreCase)) continue;
                    value = "X";
                }
                if (field.Optional && string.IsNullOrWhiteSpace(value)) continue;
                RequireValue(field.Key, value);
                var size = field.FontSize ?? definition.FontSize;
                var minimum = field.MinFontSize ?? definition.MinFontSize;
                if (field.Rows is not null)
                {
                    DrawRows(graphics, definition.Font, field, value, data, size, minimum);
                    continue;
                }
                XFont font;
                string[] lines;
                while (true)
                {
                    font = new XFont(definition.Font, size);
                    lines = Wrap(value, graphics, font, field.Width);
                    if (lines.All(l => graphics.MeasureString(l, font).Width <= field.Width) && lines.Length * size * 1.2 <= field.Height) break;
                    if (size <= minimum) throw Overflow(field.Key);
                    size = Math.Max(minimum, size - 0.5);
                }
                for (var i = 0; i < lines.Length; i++)
                    {
                    var line = lines[i];
                    var x = field.Alignment == "center" ? field.X + (field.Width - graphics.MeasureString(line, font).Width) / 2 : field.X;
                    var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (field.Alignment == "justify" && i < lines.Length - 1 && words.Length > 1)
                    {
                        var gap = (field.Width - words.Sum(w => graphics.MeasureString(w, font).Width)) / (words.Length - 1);
                        foreach (var word in words)
                        {
                            graphics.DrawString(word, font, XBrushes.Black, new XPoint(x, field.Y + size + i * size * 1.2));
                            x += graphics.MeasureString(word, font).Width + gap;
                        }
                    }
                    else graphics.DrawString(line, font, XBrushes.Black, new XPoint(x, field.Y + size + i * size * 1.2));
                }
            }
        }
        foreach (var stamp in definition.Stamps ?? []) ApplyStamp(pdf, definition, stamp, data);
        using var output = new MemoryStream();
        pdf.Save(output, false);
        return output.ToArray();
    }

    private static void DrawRows(XGraphics graphics, string family, Field field, string value,
        IReadOnlyDictionary<string, object?> data, double size, double minimum)
    {
        var rows = field.Rows!;
        var followingKey = field.FollowingKey;
        var following = followingKey is null ? null : data.GetValueOrDefault(followingKey) as string ?? "";
        if (followingKey is not null) RequireValue(followingKey, following!);

        while (true)
        {
            var font = new XFont(family, size);
            var lines = FitRows(value, rows, graphics, font);
            var failure = field.Key;
            if (lines is not null)
            {
                var position = following is null ? [] : FitRows(following, rows.AsSpan(lines.Count), graphics, font);
                if (position is not null)
                {
                    lines.AddRange(position);
                    var start = followingKey is null ? 0 : rows.Length - lines.Count;
                    for (var i = 0; i < lines.Count; i++)
                    {
                        var line = lines[i];
                        if (line.Length == 0) continue;
                        var row = rows[start + i];
                        var x = field.Alignment == "center" ? row.X + (row.Width - graphics.MeasureString(line, font).Width) / 2 : row.X;
                        graphics.DrawString(line, font, XBrushes.Black, new XPoint(x, row.Baseline));
                    }
                    return;
                }
                failure = followingKey!;
            }
            if (size <= minimum) throw Overflow(failure);
            size = Math.Max(minimum, size - 0.5);
        }
    }

    private static List<string>? FitRows(string value, ReadOnlySpan<TextRow> rows, XGraphics graphics, XFont font)
    {
        var lines = new List<string>(rows.Length);
        foreach (var paragraph in value.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var words = paragraph.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var next = 0;
            do
            {
                if (lines.Count == rows.Length) return null;
                var width = rows[lines.Count].Width;
                var line = "";
                while (next < words.Length)
                {
                    var candidate = line.Length == 0 ? words[next] : line + " " + words[next];
                    if (graphics.MeasureString(candidate, font).Width > width) break;
                    line = candidate;
                    next++;
                }
                // A token may fit the full-width row after the short first row.
                lines.Add(line);
            } while (next < words.Length);
        }
        return lines;
    }

    private static void RequireValue(string key, string value)
    {
        if (value.Length == 0)
            throw new BusinessRuleException("documents.data_missing", "Completa los datos del formato.", new Dictionary<string, string[]> { [key] = ["Dato requerido para el formato."] });
    }

    private static BusinessRuleException Overflow(string key) =>
        new("documents.text_overflow", "El texto no cabe en el formato oficial.", new Dictionary<string, string[]> { [key] = ["Reduce el texto para conservar la legibilidad del formato."] });

    private void ApplyStamp(PdfDocument pdf, TemplateDefinition definition, Stamp stamp, IReadOnlyDictionary<string, object?> data)
    {
        if (!definition.Institutional || stamp.Page < 0 || stamp.Page >= pdf.PageCount || stamp.Opacity is < 0 or > 1
            || stamp.Width <= 0 || stamp.Height <= 0 || stamp.X < 0 || stamp.Y < 0)
            throw Unavailable();
        var page = pdf.Pages[stamp.Page];
        if (stamp.X + stamp.Width > page.Width.Point || stamp.Y + stamp.Height > page.Height.Point) throw Unavailable();
        if (!data.TryGetValue("authorities", out var authorities) || authorities is null
            || !JsonSerializer.SerializeToElement(authorities).TryGetProperty(stamp.AuthorityRoleKey, out _))
            throw new ConflictException("documents.template_unavailable", "Falta la autoridad institucional activa para esta estampa.");
        var path = Path.GetFullPath(Path.Combine(Root, stamp.File));
        var relative = Path.GetRelativePath(Root, path);
        if (Path.IsPathRooted(relative) || relative.StartsWith("..") || !File.Exists(path)) throw Unavailable();
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 8 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10})
            || !Convert.ToHexString(SHA256.HashData(bytes)).Equals(stamp.Sha256, StringComparison.OrdinalIgnoreCase)) throw Unavailable();
        using var source = new MemoryStream(bytes);
        using var image = XImage.FromStream(source);
        var scale = Math.Min(stamp.Width / image.PointWidth, stamp.Height / image.PointHeight);
        var width = image.PointWidth * scale;
        var height = image.PointHeight * scale;
        using (var graphics = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append))
            graphics.DrawImage(image, stamp.X + (stamp.Width - width) / 2, stamp.Y + (stamp.Height - height) / 2, width, height);
        var states = page.Resources.Elements.GetDictionary("/ExtGState");
        if (states is null) { states = new PdfDictionary(pdf); page.Resources.Elements["/ExtGState"] = states; }
        var name = "StampAlpha" + states.Elements.Count;
        var state = new PdfDictionary(pdf);
        state.Elements["/ca"] = new PdfReal(stamp.Opacity);
        state.Elements["/CA"] = new PdfReal(stamp.Opacity);
        states.Elements["/" + name] = state;
        var content = page.Contents.Elements.GetDictionary(page.Contents.Elements.Count - 1)!;
        content.Stream.Value = [.. Encoding.ASCII.GetBytes($"q /{name} gs\n"), .. content.Stream.Value, .. "\nQ"u8.ToArray()];
    }

    private (TemplateDefinition Definition, byte[] Bytes) Load(string key)
    {
        var manifest = Path.Combine(Root, "manifest.json");
        if (!File.Exists(manifest)) throw Unavailable();
        Dictionary<string, TemplateDefinition>? definitions;
        try { definitions = JsonSerializer.Deserialize<Dictionary<string, TemplateDefinition>>(File.ReadAllText(manifest), JsonOptions); }
        catch (JsonException) { throw Unavailable(); }
        if (definitions is null || !definitions.TryGetValue(key, out var definition) || definition is null
            || string.IsNullOrWhiteSpace(definition.File) || string.IsNullOrWhiteSpace(definition.Version)
            || string.IsNullOrWhiteSpace(definition.Font) || definition.Fields is null) throw Unavailable();
        var path = Path.GetFullPath(Path.Combine(Root, definition.File));
        var relative = Path.GetRelativePath(Root, path);
        if (Path.IsPathRooted(relative) || relative.StartsWith("..") || !File.Exists(path)) throw Unavailable();
        var bytes = File.ReadAllBytes(path);
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(definition.Sha256, StringComparison.OrdinalIgnoreCase)) throw Unavailable();
        try
        {
            using var source = new MemoryStream(bytes);
            using var pdf = PdfReader.Open(source, PdfDocumentOpenMode.Import);
            ValidateLayout(definition, pdf);
        }
        catch (Exception ex) when (ex is PdfReaderException or ArgumentException or InvalidOperationException or EndOfStreamException)
        { throw Unavailable(); }
        return (definition, bytes);
    }

    private static void ValidateLayout(TemplateDefinition definition, PdfDocument pdf)
    {
        if (pdf.PageCount == 0 || definition.Pages != pdf.PageCount
            || definition.PageSizes is null || definition.PageSizes.Length != pdf.PageCount
            || !ValidFontSize(definition.FontSize, definition.MinFontSize)) throw Unavailable();
        for (var i = 0; i < pdf.PageCount; i++)
        {
            var size = definition.PageSizes[i];
            if (size is null || !double.IsFinite(size.Width) || !double.IsFinite(size.Height)
                || Math.Abs(size.Width - pdf.Pages[i].Width.Point) > 0.01
                || Math.Abs(size.Height - pdf.Pages[i].Height.Point) > 0.01) throw Unavailable();
        }
        foreach (var field in definition.Fields)
        {
            if (field is null || field.Alignment is not ("left" or "center" or "justify") || string.IsNullOrWhiteSpace(field.Key) || field.Page < 0 || field.Page >= pdf.PageCount
                || !new[] { field.X, field.Y, field.Width, field.Height }.All(double.IsFinite)
                || field.X < 0 || field.Y < 0 || field.Width <= 0 || field.Height <= 0
                || !ValidFontSize(field.FontSize ?? definition.FontSize, field.MinFontSize ?? definition.MinFontSize)) throw Unavailable();
            var page = pdf.Pages[field.Page];
            if (field.X + field.Width > page.Width.Point || field.Y + field.Height > page.Height.Point) throw Unavailable();
            if (field.FollowingKey is not null && (field.Rows is null || string.IsNullOrWhiteSpace(field.FollowingKey)
                || field.FollowingKey == field.Key || field.MatchValue is not null || field.Optional)) throw Unavailable();
            if (field.Rows is null) continue;
            if (field.Rows.Length == 0 || field.Alignment == "justify") throw Unavailable();
            var baseline = field.Y;
            foreach (var row in field.Rows)
            {
                if (row is null || !double.IsFinite(row.X) || !double.IsFinite(row.Width) || !double.IsFinite(row.Baseline)
                    || row.X < field.X || row.Width <= 0 || row.X + row.Width > field.X + field.Width + 0.001
                    || row.Baseline <= baseline || row.Baseline >= field.Y + field.Height
                    || (field.FollowingKey is not null && row.Width != field.Rows[0].Width)) throw Unavailable();
                baseline = row.Baseline;
            }
        }
    }

    private static bool ValidFontSize(double size, double minimum) =>
        double.IsFinite(size) && double.IsFinite(minimum) && minimum > 0 && size >= minimum;

    private static string[] Wrap(string value, XGraphics graphics, XFont font, double width)
    {
        var lines = new List<string>();
        foreach (var paragraph in value.Replace("\r", "").Split('\n'))
        {
            var line = "";
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && graphics.MeasureString(candidate, font).Width > width) { lines.Add(line); line = word; }
                else line = candidate;
            }
            lines.Add(line);
        }
        return lines.ToArray();
    }

    private static ConflictException Unavailable() => new("documents.template_unavailable", "La plantilla oficial aún no está disponible o no superó su verificación.");
    public sealed record TextRow(double X, double Width, double Baseline);
    public sealed record Field(string Key, double X, double Y, double Width, double Height, double? FontSize = null, double? MinFontSize = null, int Page = 0, string? Format = null, string? MatchValue = null, bool Optional = false, string Alignment = "left", TextRow[]? Rows = null, string? FollowingKey = null);
    public sealed record Stamp(string File, string Sha256, string AuthorityRoleKey, double X, double Y, double Width, double Height, double Opacity = 1, int Page = 0);
    public sealed record PageSize(double Width, double Height);
    public sealed record TemplateDefinition(string File, string Sha256, string Version, string Font, Field[] Fields, bool Institutional = false, Stamp[]? Stamps = null, int Pages = 0, PageSize[]? PageSizes = null, double FontSize = 9, double MinFontSize = 7);
}
