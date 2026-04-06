using Newtonsoft.Json.Linq;

namespace Commerce.Eventing.Infrastructure;

public sealed class CosmosEventDocument
{
    public required string id { get; init; }

    public required string documentType { get; init; }

    public required string aggregateId { get; init; }

    public required string eventType { get; init; }

    public required int eventVersion { get; init; }

    public required int sequenceNumber { get; init; }

    public required DateTime occurredAtUtc { get; init; }

    public required int schemaVersion { get; init; }

    public string? correlationId { get; init; }

    public string? causationId { get; init; }

    public required JObject payload { get; init; }
}