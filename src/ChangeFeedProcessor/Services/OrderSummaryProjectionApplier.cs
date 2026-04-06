using Commerce.Eventing.Contracts;
using Commerce.Eventing.ReadModels;

namespace ChangeFeedProcessor.Services;

internal static class OrderSummaryProjectionApplier
{
    private const string DocumentType = "order-summary";
    private const int ProjectionVersion = 1;

    public static OrderSummaryDocument Apply(OrderSummaryDocument? current, OrderEventEnvelope envelope)
    {
        if (current is not null && current.lastProcessedSequenceNumber >= envelope.SequenceNumber)
        {
            return current;
        }

        return envelope.Payload switch
        {
            OrderCreatedIntegrationEvent created => new OrderSummaryDocument
            {
                id = created.OrderId,
                documentType = DocumentType,
                orderId = created.OrderId,
                userId = created.UserId,
                status = "Pending",
                items = created.Items.ToArray(),
                totalAmount = created.Items.Sum(item => item.Quantity * item.UnitPrice),
                createdAtUtc = created.OccurredAtUtc,
                lastUpdatedAtUtc = created.OccurredAtUtc,
                lastProcessedSequenceNumber = envelope.SequenceNumber,
                projectionVersion = ProjectionVersion
            },
            OrderConfirmedIntegrationEvent confirmed when current is not null => current with
            {
                status = "Confirmed",
                lastUpdatedAtUtc = confirmed.OccurredAtUtc,
                lastProcessedSequenceNumber = envelope.SequenceNumber,
                projectionVersion = ProjectionVersion
            },
            OrderConfirmedIntegrationEvent => throw new InvalidOperationException(
                $"Cannot apply '{OrderEventTypes.OrderConfirmed}' to order '{envelope.AggregateId}' before a create projection exists."),
            _ => throw new NotSupportedException($"Unsupported order event '{envelope.EventType}' version {envelope.EventVersion}.")
        };
    }
}