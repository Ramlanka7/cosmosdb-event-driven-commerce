namespace CosmosBootstrapper.Configuration;

internal sealed class CosmosDbOptions
{
    public const string SectionName = "CosmosDb";

    public string Endpoint { get; init; } = string.Empty;

    public string Key { get; init; } = string.Empty;

    public string DatabaseName { get; init; } = string.Empty;

    public int? DatabaseManualThroughput { get; init; }

    public bool VerifyWrite { get; init; }

    public bool ResetDatabaseOnProvision { get; init; }

    public List<string> PreferredRegions { get; init; } = [];
}