using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TecNM.Api.Core.Entities;
using TecNM.Api.Core.Exceptions;
using TecNM.Api.Core.Http;
using TecNM.Api.Data;

namespace TecNM.Api.Modules.Catalogs;

public sealed partial class ActivityTypesService(TecNMDbContext dbContext)
{
    private static readonly HashSet<string> ValidAppliesTo = ["social_service", "internship", "both"];

    [GeneratedRegex(@"^[A-Z0-9_-]+$")]
    private static partial Regex CodeRegex();

    public async Task<PagedResponse<ActivityTypeResponse>> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        string? appliesTo,
        bool? isActive,
        string? sort,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.ActivityTypes.AsNoTracking();

        if (isActive.HasValue)
        {
            query = query.Where(a => a.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(appliesTo))
        {
            var norm = appliesTo.Trim().ToLowerInvariant();
            if (norm is "social_service" or "internship")
            {
                query = query.Where(a => a.AppliesTo == norm || a.AppliesTo == "both");
            }
            else if (norm == "both")
            {
                query = query.Where(a => a.AppliesTo == "both");
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(a => EF.Functions.ILike(a.Code, pattern) || EF.Functions.ILike(a.Name, pattern));
        }

        query = sort?.ToLowerInvariant() switch
        {
            "-code" => query.OrderByDescending(a => a.Code),
            "name" => query.OrderBy(a => a.Name),
            "-name" => query.OrderByDescending(a => a.Name),
            _ => query.OrderBy(a => a.Name).ThenBy(a => a.Code)
        };

        return await query
            .Select(a => new ActivityTypeResponse(
                a.Id, a.Code, a.Name, a.AppliesTo, a.Description, a.IsActive, a.CreatedAt, a.UpdatedAt))
            .ToPagedResponseAsync(page, pageSize, cancellationToken);
    }

    public async Task<IReadOnlyList<OptionItem>> GetOptionsAsync(
        string? tramiteType = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.ActivityTypes.AsNoTracking().Where(a => a.IsActive);

        if (!string.IsNullOrWhiteSpace(tramiteType))
        {
            var norm = tramiteType.Trim().ToLowerInvariant();
            if (norm is "social_service" or "internship")
            {
                query = query.Where(a => a.AppliesTo == norm || a.AppliesTo == "both");
            }
        }

        var items = await query
            .OrderBy(a => a.Name)
            .Select(a => new { a.Id, a.Code, a.Name, a.AppliesTo })
            .ToListAsync(cancellationToken);

        return items
            .Select(a => new OptionItem(
                a.Id,
                a.Name,
                new Dictionary<string, object?> { ["code"] = a.Code, ["appliesTo"] = a.AppliesTo }))
            .ToList();
    }

    public async Task<ActivityTypeResponse> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var item = await dbContext.ActivityTypes
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException("activity_types.not_found", "Tipo de actividad no encontrado.");

        return ToResponse(item);
    }

    public async Task<ActivityTypeResponse> CreateAsync(
        ActivityTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var code = request.Code.Trim().ToUpperInvariant();

        if (await dbContext.ActivityTypes.AnyAsync(a => a.Code == code, cancellationToken))
        {
            throw new ConflictException("activity_types.duplicated_code", $"Ya existe un tipo de actividad con la clave '{code}'.");
        }

        var entity = new ActivityType
        {
            Code = code,
            Name = request.Name.Trim(),
            AppliesTo = request.AppliesTo.Trim().ToLowerInvariant(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = true
        };

        dbContext.ActivityTypes.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(entity);
    }

    public async Task<ActivityTypeResponse> UpdateAsync(
        long id,
        ActivityTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var entity = await dbContext.ActivityTypes.SingleOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException("activity_types.not_found", "Tipo de actividad no encontrado.");

        var code = request.Code.Trim().ToUpperInvariant();
        if (await dbContext.ActivityTypes.AnyAsync(a => a.Id != id && a.Code == code, cancellationToken))
        {
            throw new ConflictException("activity_types.duplicated_code", $"Ya existe otro tipo de actividad con la clave '{code}'.");
        }

        entity.Code = code;
        entity.Name = request.Name.Trim();
        entity.AppliesTo = request.AppliesTo.Trim().ToLowerInvariant();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(entity);
    }

    public async Task<ActivityTypeResponse> UpdateStatusAsync(
        long id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ActivityTypes.SingleOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException("activity_types.not_found", "Tipo de actividad no encontrado.");

        entity.IsActive = isActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(entity);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ActivityTypes.SingleOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException("activity_types.not_found", "Tipo de actividad no encontrado.");

        var inUse = await dbContext.SocialServices.AnyAsync(s => s.ActivityTypeId == id, cancellationToken)
            || await dbContext.Internships.AnyAsync(i => i.ActivityTypeId == id, cancellationToken);

        if (inUse)
        {
            throw new ConflictException("activity_types.in_use", "No se puede eliminar un tipo de actividad vinculado a trámites registrados.");
        }

        dbContext.ActivityTypes.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateRequest(ActivityTypeRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Trim().Length > 32)
        {
            errors["code"] = ["La clave es obligatoria y debe tener entre 1 y 32 caracteres."];
        }
        else if (!CodeRegex().IsMatch(request.Code.Trim().ToUpperInvariant()))
        {
            errors["code"] = ["La clave solo puede contener letras mayúsculas, números, guiones y guiones bajos."];
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 150)
        {
            errors["name"] = ["El nombre es obligatorio y debe tener entre 1 y 150 caracteres."];
        }

        var normApplies = request.AppliesTo?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!ValidAppliesTo.Contains(normApplies))
        {
            errors["appliesTo"] = ["El alcance debe ser 'social_service', 'internship' o 'both'."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }

    private static ActivityTypeResponse ToResponse(ActivityType a) => new(
        a.Id, a.Code, a.Name, a.AppliesTo, a.Description, a.IsActive, a.CreatedAt, a.UpdatedAt);
}
