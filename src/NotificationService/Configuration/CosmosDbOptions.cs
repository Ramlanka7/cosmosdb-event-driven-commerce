namespace NotificationService.Configuration;

internal sealed class CosmosDbOptions
{
    public const string SectionName = "CosmosDb";

    public string Endpoint { get; init; } = string.Empty;

    public string Key { get; init; } = string.Empty;

    public string DatabaseName { get; init; } = string.Empty;

    public string OrderEventsContainerName { get; init; } = "order-events";

    public string NotificationsContainerName { get; init; } = "notifications";

    public string LeasesContainerName { get; init; } = "change-feed-leases";

    public string FailuresContainerName { get; init; } = "change-feed-failures";

    public string ProcessorName { get; init; } = "notifications-projector";

    public string InstanceName { get; init; } = "notifications-projector-local";

    public List<string> PreferredRegions { get; init; } = [];
}