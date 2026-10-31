using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace TecNM.Api.Core.Security;

public interface ICurrentUser
{
    long? Id { get; }
    string? Username { get; }
    bool IsAuthenticated { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsInRole(string role);
}

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public long? Id
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            var sub = user?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                      ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return long.TryParse(sub, CultureInfo.InvariantCulture, out var id) ? id : null;
        }
    }

    public string? Username
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            return user?.FindFirst("username")?.Value ?? user?.Identity?.Name;
        }
    }

    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public IReadOnlyList<string> Roles
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            if (user is null)
            {
                return [];
            }

            return user.FindAll("role")
                .Concat(user.FindAll(ClaimTypes.Role))
                .Select(c => c.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public bool IsInRole(string role) =>
        Roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
}
