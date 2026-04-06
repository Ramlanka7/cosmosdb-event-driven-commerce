using Commerce.Eventing.Contracts;
using Commerce.Eventing.ReadModels;

namespace NotificationService.Services;

internal static class NotificationFactory
{
    private const string DocumentType = "notification";
    private const int ProjectionVersion = 1;

    public static NotificationDocument Create(OrderEventEnvelope envelope)
    {
        (string notificationType, string message) = envelope.Payload switch
        {
            OrderCreatedIntegrationEvent created => ("order-received", $"We received order '{created.OrderId}'."),
            OrderConfirmedIntegrationEvent confirmed => ("order-confirmed", $"Order '{confirmed.OrderId}' is confirmed."),
            _ => throw new NotSupportedException($"Unsupported notification event '{envelope.EventType}' version {envelope.EventVersion}.")
        };

        return new NotificationDocument
        {
            id = $"{envelope.AggregateId}:{envelope.SequenceNumber:D10}",
            documentType = DocumentType,
            userId = envelope.Payload.UserId,
            orderId = envelope.AggregateId,
            notificationType = notificationType,
            message = message,
            occurredAtUtc = envelope.OccurredAtUtc,
            correlationId = envelope.CorrelationId,
            causationId = envelope.CausationId,
            sourceEventSequenceNumber = envelope.SequenceNumber,
            projectionVersion = ProjectionVersion
        };
    }
}