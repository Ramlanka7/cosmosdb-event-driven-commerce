namespace CosmosBootstrapper.Domain;

internal sealed class BootstrapVerificationEventDocument
{
    public string id { get; init; } = string.Empty;

    public string documentType { get; init; } = string.Empty;

    public string aggregateId { get; init; } = string.Empty;

    public string eventType { get; init; } = string.Empty;

    public int eventVersion { get; init; }

    public int sequenceNumber { get; init; }

    public DateTime occurredAtUtc { get; init; }

    public int schemaVersion { get; init; }

    public string? correlationId { get; init; }

    public string? causationId { get; init; }

    public BootstrapVerificationOrderCreated payload { get; init; } = new();
}