namespace TecNM.Api.Modules.Realtime;

public static class RealtimeGroups
{
    public static string User(long userId) => $"user:{userId}";

    public static string Staff(string tramiteType) =>
        $"staff:{tramiteType.Trim().ToLowerInvariant()}";
}

public sealed record RealtimeEventPayload(
    string EventType,
    string TramiteType,
    long? TramiteId,
    long StudentId,
    long? DocumentId,
    string? DocumentCode,
    string Status,
    string Title,
    string Message,
    string Tone,
    bool Silent,
    long? ActorUserId,
    DateTimeOffset OccurredAt);

public sealed record DocumentReviewLockDto(
    long DocumentId,
    string TramiteType,
    long ReviewerUserId,
    string ReviewerName,
    DateTimeOffset StartedAt);
