namespace Commerce.Eventing.ReadModels;

public sealed record class RecommendationDocument
{
    public required string id { get; init; }

    public required string documentType { get; init; }

    public required string userId { get; init; }

    public required IReadOnlyCollection<RecommendedSku> suggestedSkus { get; init; }

    public required DateTime lastRefreshedAtUtc { get; init; }

    public required int lastProcessedSequenceNumber { get; init; }

    public required int projectionVersion { get; init; }
}

public sealed record RecommendedSku(string Sku, decimal Score, string Reason);