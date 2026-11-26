using System.Collections.Concurrent;

namespace TecNM.Api.Modules.Realtime;

public sealed class DocumentReviewPresenceRegistry
{
    private sealed record LockEntry(
        long DocumentId,
        string TramiteType,
        long ReviewerUserId,
        string ReviewerName,
        string ConnectionId,
        DateTimeOffset StartedAt);

    private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(30);
    private readonly ConcurrentDictionary<long, LockEntry> _locks = new();

    public IReadOnlyCollection<string> AcquireOrRefresh(
        long documentId,
        string tramiteType,
        long reviewerUserId,
        string reviewerName,
        string connectionId,
        DateTimeOffset now)
    {
        var affectedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            tramiteType.Trim().ToLowerInvariant()
        };

        // Release any other document previously held by the same connection (1 modal open per tab).
        foreach (var kvp in _locks)
        {
            if (kvp.Key != documentId && kvp.Value.ConnectionId == connectionId)
            {
                if (_locks.TryRemove(kvp.Key, out var removed))
                {
                    affectedTypes.Add(removed.TramiteType);
                }
            }
        }

        var normalizedType = tramiteType.Trim().ToLowerInvariant();
        var nextEntry = new LockEntry(
            documentId,
            normalizedType,
            reviewerUserId,
            reviewerName,
            connectionId,
            now);

        _locks.AddOrUpdate(
            documentId,
            nextEntry,
            (_, existing) =>
            {
                var isSameReviewer = existing.ReviewerUserId == reviewerUserId || existing.ConnectionId == connectionId;
                var isStale = now - existing.StartedAt > StaleThreshold;
                return isSameReviewer || isStale ? nextEntry : existing;
            });

        return affectedTypes;
    }

    public string? ReleaseByConnectionAndDocument(string connectionId, long documentId)
    {
        if (_locks.TryGetValue(documentId, out var existing) && existing.ConnectionId == connectionId)
        {
            if (_locks.TryRemove(documentId, out var removed))
            {
                return removed.TramiteType;
            }
        }

        return null;
    }

    public IReadOnlyCollection<string> ReleaseAllByConnection(string connectionId)
    {
        var affectedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in _locks)
        {
            if (kvp.Value.ConnectionId == connectionId && _locks.TryRemove(kvp.Key, out var removed))
            {
                affectedTypes.Add(removed.TramiteType);
            }
        }

        return affectedTypes;
    }

    public string? ReleaseDocument(long documentId) =>
        _locks.TryRemove(documentId, out var removed) ? removed.TramiteType : null;

    public IReadOnlyList<DocumentReviewLockDto> GetLocksForTramiteType(
        string tramiteType,
        DateTimeOffset? now = null)
    {
        var normalized = tramiteType.Trim().ToLowerInvariant();
        var cutoff = (now ?? DateTimeOffset.UtcNow) - StaleThreshold;
        return _locks.Values
            .Where(e => e.TramiteType == normalized && e.StartedAt >= cutoff)
            .OrderBy(e => e.DocumentId)
            .Select(ToDto)
            .ToList();
    }

    public IReadOnlyList<DocumentReviewLockDto> GetLocksForTramiteTypes(
        IEnumerable<string> tramiteTypes,
        DateTimeOffset? now = null)
    {
        var allowed = new HashSet<string>(
            tramiteTypes.Select(t => t.Trim().ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);
        var cutoff = (now ?? DateTimeOffset.UtcNow) - StaleThreshold;

        return _locks.Values
            .Where(e => allowed.Contains(e.TramiteType) && e.StartedAt >= cutoff)
            .OrderBy(e => e.DocumentId)
            .Select(ToDto)
            .ToList();
    }

    private static DocumentReviewLockDto ToDto(LockEntry entry) =>
        new(entry.DocumentId, entry.TramiteType, entry.ReviewerUserId, entry.ReviewerName, entry.StartedAt);
}
