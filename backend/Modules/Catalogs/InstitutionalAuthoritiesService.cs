using Microsoft.EntityFrameworkCore;
using TecNM.Api.Core.Entities;
using TecNM.Api.Core.Exceptions;
using TecNM.Api.Core.Http;
using TecNM.Api.Data;

namespace TecNM.Api.Modules.Catalogs;

public class InstitutionalAuthoritiesService(TecNMDbContext dbContext)
{
    private static readonly HashSet<string> ValidRoleKeys =
    [
        "institute_director",
        "outreach_division_head",
        "social_service_head",
        "internship_coordinator"
    ];

    public async Task<PagedResponse<InstitutionalAuthorityResponse>> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        string? roleKey,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.InstitutionalAuthorities.AsNoTracking();

        if (isActive.HasValue)
        {
            query = query.Where(a => a.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(roleKey))
        {
            query = query.Where(a => a.RoleKey == roleKey.Trim().ToLowerInvariant());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(a => EF.Functions.ILike(a.FullName, pattern) || EF.Functions.ILike(a.Position, pattern));
        }

        query = query.OrderBy(a => a.RoleKey).ThenBy(a => a.FullName);

        var projected = query.Select(a => new InstitutionalAuthorityResponse(
            a.Id,
            a.RoleKey,
            a.Title,
            a.FullName,
            a.Position,
            a.Email,
            a.PhoneNumber,
            a.SignatureInitials,
            a.IsActive,
            a.CreatedAt,
            a.UpdatedAt));

        return await projected.ToPagedResponseAsync(page, pageSize, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, InstitutionalAuthorityResponse?>> GetActiveMapAsync(
        CancellationToken cancellationToken = default)
    {
        var activeAuthorities = await dbContext.InstitutionalAuthorities
            .AsNoTracking()
            .Where(a => a.IsActive)
            .ToListAsync(cancellationToken);

        var result = new Dictionary<string, InstitutionalAuthorityResponse?>();
        foreach (var key in ValidRoleKeys)
        {
            var match = activeAuthorities.FirstOrDefault(a => a.RoleKey == key);
            result[key] = match is null ? null : ToResponse(match);
        }

        return result;
    }

    public async Task<InstitutionalAuthorityResponse> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var authority = await dbContext.InstitutionalAuthorities
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (authority is null)
        {
            throw new NotFoundException("institutional_authorities.not_found", "Autoridad institucional no encontrada.");
        }

        return ToResponse(authority);
    }

    public async Task<InstitutionalAuthorityResponse> CreateAsync(
        InstitutionalAuthorityRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var normalizedRoleKey = request.RoleKey.Trim().ToLowerInvariant();

        // Si se crea como activa (por defecto true), desactivar la previa del mismo role_key
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var existingActives = await dbContext.InstitutionalAuthorities
            .Where(a => a.RoleKey == normalizedRoleKey && a.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var a in existingActives)
        {
            a.IsActive = false;
        }
        await SaveAsync(cancellationToken);

        var authority = new InstitutionalAuthority
        {
            RoleKey = normalizedRoleKey,
            Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(),
            FullName = request.FullName.Trim(),
            Position = request.Position.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            SignatureInitials = string.IsNullOrWhiteSpace(request.SignatureInitials) ? null : request.SignatureInitials.Trim().ToUpperInvariant(),
            IsActive = true
        };

        dbContext.InstitutionalAuthorities.Add(authority);
        await SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToResponse(authority);
    }

    public async Task<InstitutionalAuthorityResponse> UpdateAsync(
        long id,
        InstitutionalAuthorityRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var authority = await dbContext.InstitutionalAuthorities
            .SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (authority is null)
        {
            throw new NotFoundException("institutional_authorities.not_found", "Autoridad institucional no encontrada.");
        }

        authority.RoleKey = request.RoleKey.Trim().ToLowerInvariant();
        authority.Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim();
        authority.FullName = request.FullName.Trim();
        authority.Position = request.Position.Trim();
        authority.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();
        authority.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        authority.SignatureInitials = string.IsNullOrWhiteSpace(request.SignatureInitials) ? null : request.SignatureInitials.Trim().ToUpperInvariant();

        await SaveAsync(cancellationToken);

        return ToResponse(authority);
    }

    public async Task<InstitutionalAuthorityResponse> UpdateStatusAsync(
        long id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var authority = await dbContext.InstitutionalAuthorities
            .SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (authority is null)
        {
            throw new NotFoundException("institutional_authorities.not_found", "Autoridad institucional no encontrada.");
        }

        if (isActive)
        {
            // Desactivar previa del mismo role_key
            var prevActives = await dbContext.InstitutionalAuthorities
                .Where(a => a.Id != id && a.RoleKey == authority.RoleKey && a.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var a in prevActives)
            {
                a.IsActive = false;
            }
            await SaveAsync(cancellationToken);
        }

        authority.IsActive = isActive;
        await SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToResponse(authority);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var authority = await dbContext.InstitutionalAuthorities
            .SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (authority is null)
        {
            throw new NotFoundException("institutional_authorities.not_found", "Autoridad institucional no encontrada.");
        }

        dbContext.InstitutionalAuthorities.Remove(authority);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateRequest(InstitutionalAuthorityRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.RoleKey) || !ValidRoleKeys.Contains(request.RoleKey.Trim().ToLowerInvariant()))
        {
            errors["roleKey"] = [$"El rol institucional debe ser uno de los autorizados: {string.Join(", ", ValidRoleKeys)}."];
        }

        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length > 180)
        {
            errors["fullName"] = ["El nombre completo es obligatorio y debe tener entre 1 y 180 caracteres."];
        }

        if (request.Title?.Trim().Length > 32)
        {
            errors["title"] = ["El título debe tener hasta 32 caracteres."];
        }

        if (string.IsNullOrWhiteSpace(request.Position) || request.Position.Trim().Length > 180)
        {
            errors["position"] = ["El cargo o puesto es obligatorio y debe tener entre 1 y 180 caracteres."];
        }

        if (!string.IsNullOrWhiteSpace(request.Email) && !request.Email.Contains('@'))
        {
            errors["email"] = ["El formato del correo electrónico es inválido."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try { await dbContext.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException
            { ConstraintName: "uq_institutional_authorities_active_role" })
        {
            throw new ConflictException("institutional_authorities.active_conflict", "El titular activo cambió. Actualiza el catálogo.");
        }
    }

    private static InstitutionalAuthorityResponse ToResponse(InstitutionalAuthority a) => new(
        a.Id,
        a.RoleKey,
        a.Title,
        a.FullName,
        a.Position,
        a.Email,
        a.PhoneNumber,
        a.SignatureInitials,
        a.IsActive,
        a.CreatedAt,
        a.UpdatedAt);
}
