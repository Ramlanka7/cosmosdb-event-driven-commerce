namespace ReadModelService.Configuration;

internal sealed class CosmosDbOptions
{
    public const string SectionName = "CosmosDb";

    public string Endpoint { get; init; } = string.Empty;

    public string Key { get; init; } = string.Empty;

    public string DatabaseName { get; init; } = string.Empty;

    public string OrdersReadContainerName { get; init; } = "orders-read";

    public List<string> PreferredRegions { get; init; } = [];
}