namespace CosmosBootstrapper.Domain;

internal sealed class BootstrapVerificationUser
{
    public string id { get; init; } = string.Empty;

    public string userId { get; init; } = string.Empty;

    public string displayName { get; init; } = string.Empty;

    public DateTime createdAtUtc { get; init; }
}