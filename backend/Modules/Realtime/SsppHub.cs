using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TecNM.Api.Data;

namespace TecNM.Api.Modules.Realtime;

[Authorize]
public sealed class SsppHub(
    TecNMDbContext dbContext,
    DocumentReviewPresenceRegistry presenceRegistry,
    TimeProvider clock) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.User(userId.Value));
        }

        var roles = GetRoles();
        var staffTypes = GetAuthorizedStaffTypes(roles);
        foreach (var tramiteType in staffTypes)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Staff(tramiteType));
        }

        if (staffTypes.Count > 0)
        {
            var activeLocks = presenceRegistry.GetLocksForTramiteTypes(staffTypes);
            await Clients.Caller.SendAsync("ReviewPresenceSnapshot", activeLocks);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var affectedTypes = presenceRegistry.ReleaseAllByConnection(Context.ConnectionId);
        foreach (var tramiteType in affectedTypes)
        {
            var locks = presenceRegistry.GetLocksForTramiteType(tramiteType);
            await Clients.Group(RealtimeGroups.Staff(tramiteType))
                .SendAsync("ReviewPresenceUpdated", tramiteType, locks);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task StartReviewing(long documentId)
    {
        if (documentId <= 0) return;
        var userId = GetUserId();
        if (!userId.HasValue) return;

        var roles = GetRoles();
        if (!CanReviewDocuments(roles)) return;

        var doc = await dbContext.ExpedientDocuments
            .AsNoTracking()
            .Where(d => d.Id == documentId && d.Status == "pending")
            .Select(d => new { d.Id, d.TramiteType })
            .SingleOrDefaultAsync(Context.ConnectionAborted);

        if (doc is null || !CanReviewTramiteType(roles, doc.TramiteType))
        {
            return;
        }

        var affectedTypes = presenceRegistry.AcquireOrRefresh(
            doc.Id,
            doc.TramiteType,
            userId.Value,
            GetDisplayName(),
            Context.ConnectionId,
            clock.GetUtcNow());

        foreach (var tramiteType in affectedTypes)
        {
            var locks = presenceRegistry.GetLocksForTramiteType(tramiteType);
            await Clients.Group(RealtimeGroups.Staff(tramiteType))
                .SendAsync("ReviewPresenceUpdated", tramiteType, locks);
        }
    }

    public async Task StopReviewing(long documentId)
    {
        if (documentId <= 0) return;
        var releasedType = presenceRegistry.ReleaseByConnectionAndDocument(Context.ConnectionId, documentId);
        if (releasedType is not null)
        {
            var locks = presenceRegistry.GetLocksForTramiteType(releasedType);
            await Clients.Group(RealtimeGroups.Staff(releasedType))
                .SendAsync("ReviewPresenceUpdated", releasedType, locks);
        }
    }

    private long? GetUserId()
    {
        var user = Context.User;
        var sub = user?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(sub, CultureInfo.InvariantCulture, out var id) ? id : null;
    }

    private HashSet<string> GetRoles()
    {
        var user = Context.User;
        if (user is null) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return user.FindAll("role")
            .Concat(user.FindAll(ClaimTypes.Role))
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private string GetDisplayName()
    {
        var user = Context.User;
        var username = user?.FindFirst("username")?.Value;
        if (!string.IsNullOrWhiteSpace(username)) return $"@{username}";

        var name = user?.FindFirst("name")?.Value ?? user?.Identity?.Name;
        return string.IsNullOrWhiteSpace(name) ? "Vinculación" : name;
    }

    private static IReadOnlyCollection<string> GetAuthorizedStaffTypes(HashSet<string> roles)
    {
        var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (roles.Contains("department_head"))
        {
            types.Add("social_service");
            types.Add("internship");
            types.Add("general");
            return types;
        }

        if (roles.Contains("social_service_officer"))
        {
            types.Add("social_service");
            types.Add("general");
        }

        if (roles.Contains("internship_coordinator"))
        {
            types.Add("internship");
            types.Add("general");
        }

        return types;
    }

    private static bool CanReviewDocuments(HashSet<string> roles) =>
        roles.Contains("department_head")
        || roles.Contains("social_service_officer")
        || roles.Contains("internship_coordinator");

    private static bool CanReviewTramiteType(HashSet<string> roles, string tramiteType)
    {
        if (roles.Contains("department_head")) return true;
        var normalized = tramiteType.Trim().ToLowerInvariant();
        if (normalized == "social_service") return roles.Contains("social_service_officer");
        if (normalized == "internship") return roles.Contains("internship_coordinator");
        if (normalized == "general")
        {
            return roles.Contains("social_service_officer") || roles.Contains("internship_coordinator");
        }

        return false;
    }
}
