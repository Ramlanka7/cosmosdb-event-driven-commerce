using ReadModelService.Infrastructure;

namespace ReadModelService.Contracts;

internal sealed class ListOrderSummariesRequest
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    public static IReadOnlyCollection<string> SupportedStatuses { get; } = ["Pending", "Confirmed"];

    public string? Status { get; init; }

    public DateTime? UpdatedAfterUtc { get; init; }

    public DateTime? UpdatedBeforeUtc { get; init; }

    public int? PageSize { get; init; }

    public string? ContinuationToken { get; init; }

    public OrderSummaryQuery ToQuery(string userId)
    {
        _ = TryNormalizeStatus(Status, out string? normalizedStatus);

        return new OrderSummaryQuery(
            userId,
            normalizedStatus,
            UpdatedAfterUtc,
            UpdatedBeforeUtc,
            PageSize ?? DefaultPageSize,
            string.IsNullOrWhiteSpace(ContinuationToken) ? null : ContinuationToken);
    }

    public static bool TryNormalizeStatus(string? status, out string? normalizedStatus)
    {
        normalizedStatus = null;

        if (string.IsNullOrWhiteSpace(status))
        {
            return true;
        }

        normalizedStatus = SupportedStatuses.FirstOrDefault(candidate =>
            string.Equals(candidate, status, StringComparison.OrdinalIgnoreCase));

        return normalizedStatus is not null;
    }
}