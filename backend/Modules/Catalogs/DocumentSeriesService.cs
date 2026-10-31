using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TecNM.Api.Core.Entities;
using TecNM.Api.Core.Exceptions;
using TecNM.Api.Data;

namespace TecNM.Api.Modules.Catalogs;

public partial class DocumentSeriesService(TecNMDbContext dbContext)
{
    private static readonly HashSet<string> ValidDocumentTypes =
    [
        "social_service_presentation_letter",
        "internship_presentation_letter",
        "social_service_release_certificate",
        "internship_release_certificate",
        "school_services_act"
    ];

    [GeneratedRegex(@"\{SEQ(?::(\d+))?\}")]
    private static partial Regex SeqRegex();

    public async Task<IReadOnlyList<DocumentSeriesResponse>> GetListAsync(
        string? documentType,
        int? currentYear,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.DocumentSeries.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(documentType))
        {
            query = query.Where(s => s.DocumentType == documentType.Trim().ToLowerInvariant());
        }

        if (currentYear.HasValue)
        {
            query = query.Where(s => s.CurrentYear == currentYear.Value);
        }

        var list = await query
            .OrderByDescending(s => s.CurrentYear)
            .ThenBy(s => s.DocumentType)
            .ToListAsync(cancellationToken);

        return list.Select(ToResponse).ToList();
    }

    public async Task<DocumentSeriesResponse> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var series = await dbContext.DocumentSeries
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (series is null)
        {
            throw new NotFoundException("document_series.not_found", "Serie de folios no encontrada.");
        }

        return ToResponse(series);
    }

    public async Task<DocumentSeriesResponse> CreateAsync(
        DocumentSeriesRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var normType = request.DocumentType.Trim().ToLowerInvariant();
        var exists = await dbContext.DocumentSeries
            .AnyAsync(s => s.DocumentType == normType && s.CurrentYear == request.CurrentYear, cancellationToken);

        if (exists)
        {
            throw new ConflictException("document_series.duplicated_type_year",
                $"Ya existe una serie de folios para el tipo '{normType}' en el año {request.CurrentYear}.");
        }

        var series = new DocumentSeries
        {
            DocumentType = normType,
            Prefix = request.Prefix.Trim().ToUpperInvariant(),
            CurrentYear = request.CurrentYear,
            CurrentSequence = request.CurrentSequence,
            FormatPattern = request.FormatPattern.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim()
        };

        dbContext.DocumentSeries.Add(series);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(series);
    }

    public async Task<DocumentSeriesResponse> UpdateAsync(
        long id,
        DocumentSeriesRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var series = await dbContext.DocumentSeries
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (series is null)
        {
            throw new NotFoundException("document_series.not_found", "Serie de folios no encontrada.");
        }

        // Regla: no permitir reducir la secuencia actual (rollback protection)
        if (request.CurrentSequence < series.CurrentSequence)
        {
            throw new BusinessRuleException(
                "document_series.sequence_rollback",
                $"No se permite reducir la secuencia actual ({series.CurrentSequence}) a un valor menor ({request.CurrentSequence}).");
        }

        series.Prefix = request.Prefix.Trim().ToUpperInvariant();
        series.CurrentSequence = request.CurrentSequence;
        series.FormatPattern = request.FormatPattern.Trim();
        series.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(series);
    }

    /// <summary>
    /// Consume e incrementa el folio de forma atómica bajo una transacción.
    /// Si no existe serie para el año en curso, crea la del nuevo año copiando prefijo y patrón del año anterior.
    /// </summary>
    public async Task<string> GenerateNextFolioAsync(
        string documentType,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var currentYear = now.Year;
        var normType = documentType.Trim().ToLowerInvariant();

        var series = await dbContext.DocumentSeries
            .SingleOrDefaultAsync(s => s.DocumentType == normType && s.CurrentYear == currentYear, cancellationToken);

        if (series is null)
        {
            // Auto-crear para el año en curso copiando del año anterior
            var previous = await dbContext.DocumentSeries
                .OrderByDescending(s => s.CurrentYear)
                .FirstOrDefaultAsync(s => s.DocumentType == normType, cancellationToken);

            series = new DocumentSeries
            {
                DocumentType = normType,
                Prefix = previous?.Prefix ?? "FOLIO",
                CurrentYear = currentYear,
                CurrentSequence = 0,
                FormatPattern = previous?.FormatPattern ?? "{PREFIX}{SEQ:4}/{DD}/{MM}/{YYYY}",
                Description = $"Serie automática {currentYear}"
            };

            dbContext.DocumentSeries.Add(series);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        series.CurrentSequence++;
        await dbContext.SaveChangesAsync(cancellationToken);

        return FormatFolio(series.FormatPattern, series.Prefix, series.CurrentSequence, now);
    }

    public static string FormatFolio(string pattern, string prefix, int sequence, DateTime date)
    {
        var result = pattern
            .Replace("{PREFIX}", prefix, StringComparison.OrdinalIgnoreCase)
            .Replace("{DD}", date.ToString("dd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{MM}", date.ToString("MM"), StringComparison.OrdinalIgnoreCase)
            .Replace("{YYYY}", date.ToString("yyyy"), StringComparison.OrdinalIgnoreCase);

        result = SeqRegex().Replace(result, match =>
        {
            var padGroup = match.Groups[1].Value;
            var padding = int.TryParse(padGroup, out var p) ? p : 4;
            return sequence.ToString().PadLeft(padding, '0');
        });

        return result;
    }

    private static void ValidateRequest(DocumentSeriesRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.DocumentType) || !ValidDocumentTypes.Contains(request.DocumentType.Trim().ToLowerInvariant()))
        {
            errors["documentType"] = [$"El tipo de documento debe ser uno de: {string.Join(", ", ValidDocumentTypes)}."];
        }

        if (string.IsNullOrWhiteSpace(request.Prefix) || request.Prefix.Trim().Length > 20)
        {
            errors["prefix"] = ["El prefijo es obligatorio y debe tener entre 1 y 20 caracteres."];
        }

        if (request.CurrentYear < 2020)
        {
            errors["currentYear"] = ["El año debe ser mayor o igual a 2020."];
        }

        if (request.CurrentSequence < 0)
        {
            errors["currentSequence"] = ["La secuencia actual no puede ser negativa."];
        }

        if (string.IsNullOrWhiteSpace(request.FormatPattern) || request.FormatPattern.Trim().Length > 100)
        {
            errors["formatPattern"] = ["El patrón de formato es obligatorio y debe tener entre 1 y 100 caracteres."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }

    private static DocumentSeriesResponse ToResponse(DocumentSeries s)
    {
        var preview = FormatFolio(s.FormatPattern, s.Prefix, s.CurrentSequence + 1, DateTime.UtcNow);

        return new DocumentSeriesResponse(
            s.Id,
            s.DocumentType,
            s.Prefix,
            s.CurrentYear,
            s.CurrentSequence,
            s.FormatPattern,
            s.Description,
            preview,
            s.CreatedAt,
            s.UpdatedAt);
    }
}
