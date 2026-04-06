namespace ChangeFeedProcessor.Configuration;

internal sealed class CosmosDbOptions
{
    public const string SectionName = "CosmosDb";

    public string Endpoint { get; init; } = string.Empty;

    public string Key { get; init; } = string.Empty;

    public string DatabaseName { get; init; } = string.Empty;

    public string OrderEventsContainerName { get; init; } = "order-events";

    public string OrdersReadContainerName { get; init; } = "orders-read";

    public string RecommendationsContainerName { get; init; } = "recommendations";

    public string LeasesContainerName { get; init; } = "change-feed-leases";

    public string FailuresContainerName { get; init; } = "change-feed-failures";

    public string ProcessorName { get; init; } = "orders-read-projector";

    public string InstanceName { get; init; } = "orders-read-projector-local";

    public List<string> PreferredRegions { get; init; } = [];
}