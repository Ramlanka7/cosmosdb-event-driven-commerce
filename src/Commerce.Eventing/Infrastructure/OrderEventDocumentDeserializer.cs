using System.Text.Json;
using Commerce.Eventing.Abstractions;
using Commerce.Eventing.Contracts;

namespace Commerce.Eventing.Infrastructure;

public static class OrderEventDocumentDeserializer
{
    public static OrderEventEnvelope Deserialize(CosmosEventDocument document, JsonSerializerOptions serializerOptions)
    {
        string payloadJson = document.payload.ToString(Newtonsoft.Json.Formatting.None);

        IOrderEventPayload payload = (document.eventType, document.eventVersion) switch
        {
            (OrderEventTypes.OrderCreated, 1) => System.Text.Json.JsonSerializer.Deserialize<OrderCreatedIntegrationEvent>(payloadJson, serializerOptions)
                ?? throw new InvalidOperationException("Unable to deserialize order-created payload."),
            (OrderEventTypes.OrderConfirmed, 1) => System.Text.Json.JsonSerializer.Deserialize<OrderConfirmedIntegrationEvent>(payloadJson, serializerOptions)
                ?? throw new InvalidOperationException("Unable to deserialize order-confirmed payload."),
            _ => throw new NotSupportedException($"Unsupported order event '{document.eventType}' version {document.eventVersion}.")
        };

        return new OrderEventEnvelope(
            document.aggregateId,
            document.sequenceNumber,
            document.eventType,
            document.eventVersion,
            document.occurredAtUtc,
            document.correlationId,
            document.causationId,
            payload);
    }
}