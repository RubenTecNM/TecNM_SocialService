namespace TecNM.Api.Modules.Reports;

internal static class ReportMetricRules
{
    public static readonly string[] AllStatuses =
    [
        "requested",
        "approved",
        "accepted",
        "active",
        "completed_pending_review",
        "released",
        "cancelled",
        "non_compliant"
    ];

    public static readonly HashSet<string> InProgressStatuses =
    [
        "approved",
        "accepted",
        "active",
        "completed_pending_review"
    ];

    public static readonly HashSet<string> AllNonCancelledStatuses =
    [
        "requested",
        "approved",
        "accepted",
        "active",
        "completed_pending_review",
        "released",
        "non_compliant"
    ];

    public static int StatusPriority(string status) => status switch
    {
        "released" => 5,
        "completed_pending_review" => 4,
        "active" => 4,
        "accepted" => 3,
        "approved" => 3,
        "requested" => 2,
        "non_compliant" => 1,
        "cancelled" => 0,
        _ => 0
    };

    public static HashSet<string> ResolveAllowedStatuses(string? statusGroup, string? explicitStatus)
    {
        if (!string.IsNullOrWhiteSpace(explicitStatus))
        {
            var statuses = explicitStatus
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => s.ToLowerInvariant())
                .Where(s => AllStatuses.Contains(s))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (statuses.Count > 0)
            {
                return statuses;
            }
        }

        return (statusGroup?.Trim().ToLowerInvariant()) switch
        {
            "all_non_cancelled" => [.. AllNonCancelledStatuses],
            "released" => ["released"],
            "all" => [.. AllStatuses],
            _ => [.. InProgressStatuses]
        };
    }

    public static bool IsSocialServiceReleased(string status, bool ctApproved, string? releaseCertificateFolio, DateOnly? terminationLetterDate) =>
        status == "released"
        || ctApproved
        || !string.IsNullOrWhiteSpace(releaseCertificateFolio)
        || terminationLetterDate.HasValue;

    public static bool IsInternshipReleased(string status, bool caApproved, bool ccApproved, bool isExpedientComplete, string? releaseCertificateFolio) =>
        status == "released"
        || (caApproved && (ccApproved || isExpedientComplete || !string.IsNullOrWhiteSpace(releaseCertificateFolio)));
}
