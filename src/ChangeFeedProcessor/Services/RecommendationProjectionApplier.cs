using Commerce.Eventing.Contracts;
using Commerce.Eventing.ReadModels;

namespace ChangeFeedProcessor.Services;

internal static class RecommendationProjectionApplier
{
    private const string DocumentType = "recommendation-profile";
    private const int ProjectionVersion = 2;

    public static RecommendationDocument Apply(RecommendationDocument? current, OrderEventEnvelope envelope)
    {
        Dictionary<string, int> streamCheckpoints = current?.streamCheckpoints?
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (streamCheckpoints.TryGetValue(envelope.AggregateId, out int lastProcessedSequenceNumber) &&
            lastProcessedSequenceNumber >= envelope.SequenceNumber)
        {
            return current ?? CreateEmpty(envelope.Payload.UserId);
        }

        Dictionary<string, RecommendedSku> suggestions = current?.suggestedSkus
            .ToDictionary(item => item.Sku, item => item, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, RecommendedSku>(StringComparer.OrdinalIgnoreCase);

        if (envelope.Payload is OrderCreatedIntegrationEvent created)
        {
            foreach (OrderItemData item in created.Items)
            {
                decimal existingScore = suggestions.TryGetValue(item.Sku, out RecommendedSku? existing)
                    ? existing.Score
                    : 0;

                suggestions[item.Sku] = new RecommendedSku(
                    item.Sku,
                    existingScore + (item.Quantity * item.UnitPrice),
                    "Purchased in prior orders");
            }
        }

        streamCheckpoints[envelope.AggregateId] = envelope.SequenceNumber;

        return new RecommendationDocument
        {
            id = envelope.Payload.UserId,
            documentType = DocumentType,
            userId = envelope.Payload.UserId,
            suggestedSkus = suggestions.Values
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Sku, StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToArray(),
            lastRefreshedAtUtc = envelope.OccurredAtUtc,
            lastProcessedSequenceNumber = envelope.SequenceNumber,
            projectionVersion = ProjectionVersion,
            streamCheckpoints = streamCheckpoints
        };
    }

    private static RecommendationDocument CreateEmpty(string userId) => new()
    {
        id = userId,
        documentType = DocumentType,
        userId = userId,
        suggestedSkus = [],
        lastRefreshedAtUtc = DateTime.MinValue,
        lastProcessedSequenceNumber = 0,
        projectionVersion = ProjectionVersion,
        streamCheckpoints = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    };
}