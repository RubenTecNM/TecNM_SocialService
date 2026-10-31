using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using TecNM.Api.Core.Exceptions;

namespace TecNM.Api.Core.Storage;

public interface IFileStorage
{
    Task<string> SavePdfAsync(
        IFormFile file,
        string relativeDirectory,
        string? customFileNameWithoutExtension = null,
        long maxSizeBytes = 5 * 1024 * 1024,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);

    void TryDelete(string? relativePath);
}

public sealed class LocalFileStorage(IConfiguration configuration) : IFileStorage
{
    private static readonly byte[] PdfMagicBytes = [0x25, 0x50, 0x44, 0x46]; // %PDF

    private string RootPath =>
        configuration["Storage:RootPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "storage");

    public async Task<string> SavePdfAsync(
        IFormFile file,
        string relativeDirectory,
        string? customFileNameWithoutExtension = null,
        long maxSizeBytes = 5 * 1024 * 1024,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["file"] = ["El archivo proporcionado está vacío o no fue adjuntado."]
            });
        }

        if (file.Length > maxSizeBytes)
        {
            var maxMb = Math.Round(maxSizeBytes / (1024.0 * 1024.0), 1);
            throw new AppException(
                StatusCodes.Status413PayloadTooLarge,
                "files.too_large",
                $"El archivo supera el tamaño máximo permitido de {maxMb} MB.",
                "Carga de archivo excedida");
        }

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new AppException(
                StatusCodes.Status415UnsupportedMediaType,
                "files.unsupported_type",
                "El documento debe ser un archivo en formato PDF (.pdf).",
                "Tipo de archivo no permitido");
        }

        await using (var readStream = file.OpenReadStream())
        {
            var header = new byte[4];
            var bytesRead = await readStream.ReadAsync(header.AsMemory(0, 4), cancellationToken);
            if (bytesRead < 4 || !header.SequenceEqual(PdfMagicBytes))
            {
                throw new AppException(
                    StatusCodes.Status415UnsupportedMediaType,
                    "files.unsupported_type",
                    "El archivo no contiene un encabezado PDF válido (%PDF).",
                    "Tipo de archivo no permitido");
            }
        }

        var normalizedRelativeDir = relativeDirectory
            .Replace('\\', '/')
            .Trim('/');

        var targetDirectory = Path.Combine(
            RootPath,
            normalizedRelativeDir.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(targetDirectory);

        var fileNameBase = string.IsNullOrWhiteSpace(customFileNameWithoutExtension)
            ? Guid.NewGuid().ToString("N")
            : customFileNameWithoutExtension.Trim();

        var fileName = $"{fileNameBase}.pdf";
        var relativePath = string.IsNullOrEmpty(normalizedRelativeDir)
            ? fileName
            : $"{normalizedRelativeDir}/{fileName}";

        var fullPath = Path.Combine(targetDirectory, fileName);

        await using (var targetStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(targetStream, cancellationToken);
        }

        return relativePath;
    }

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new NotFoundException("documents.file_missing", "La ruta de almacenamiento del documento no es válida.");
        }

        var normalizedRelative = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);

        var fullPath = Path.GetFullPath(Path.Combine(RootPath, normalizedRelative));
        var rootFullPath = Path.GetFullPath(RootPath);

        if (!fullPath.StartsWith(rootFullPath, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
        {
            throw new NotFoundException("documents.file_missing", "El archivo físico del documento no fue encontrado en el servidor.");
        }

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public void TryDelete(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        try
        {
            var normalizedRelative = relativePath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);

            var fullPath = Path.GetFullPath(Path.Combine(RootPath, normalizedRelative));
            var rootFullPath = Path.GetFullPath(RootPath);

            if (fullPath.StartsWith(rootFullPath, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch
        {
            // Ignorar errores al limpiar archivos antiguos reemplazados
        }
    }
}

