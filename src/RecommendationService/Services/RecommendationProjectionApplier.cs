using Commerce.Eventing.Contracts;
using Commerce.Eventing.ReadModels;

namespace RecommendationService.Services;

internal static class RecommendationProjectionApplier
{
    private const string DocumentType = "recommendation-profile";
    private const int ProjectionVersion = 1;

    public static RecommendationDocument Apply(RecommendationDocument? current, OrderEventEnvelope envelope)
    {
        if (current is not null && current.lastProcessedSequenceNumber >= envelope.SequenceNumber)
        {
            return current;
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
            projectionVersion = ProjectionVersion
        };
    }
}