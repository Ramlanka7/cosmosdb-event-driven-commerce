using System.Text.Json;
using OrderService.Domain;

namespace OrderService.Infrastructure;

internal interface IOrderEventDocumentMapper
{
    CosmosEventDocument ToDocument(string aggregateId, int sequenceNumber, IOrderEvent orderEvent, EventMetadata metadata);

    StoredOrderEvent ToStoredEvent(CosmosEventDocument document);
}

internal sealed class OrderEventDocumentMapper(JsonSerializerOptions serializerOptions) : IOrderEventDocumentMapper
{
    private const string EventDocumentType = "event";
    private const int SchemaVersion = 1;

    public CosmosEventDocument ToDocument(string aggregateId, int sequenceNumber, IOrderEvent orderEvent, EventMetadata metadata)
    {
        return new CosmosEventDocument
        {
            id = $"{aggregateId}:{sequenceNumber:D10}",
            documentType = EventDocumentType,
            aggregateId = aggregateId,
            eventType = orderEvent.EventType,
            eventVersion = orderEvent.EventVersion,
            sequenceNumber = sequenceNumber,
            occurredAtUtc = orderEvent.OccurredAtUtc,
            correlationId = metadata.CorrelationId,
            causationId = metadata.CausationId,
            schemaVersion = SchemaVersion,
            payload = JsonSerializer.SerializeToElement((object)orderEvent, orderEvent.GetType(), serializerOptions)
        };
    }

    public StoredOrderEvent ToStoredEvent(CosmosEventDocument document)
    {
        IOrderEvent payload = (document.eventType, document.eventVersion) switch
        {
            (OrderEventTypes.OrderCreated, 1) => document.payload.Deserialize<OrderCreated>(serializerOptions)
                ?? throw new InvalidOperationException("Unable to deserialize order-created event payload."),
            (OrderEventTypes.OrderConfirmed, 1) => document.payload.Deserialize<OrderConfirmed>(serializerOptions)
                ?? throw new InvalidOperationException("Unable to deserialize order-confirmed event payload."),
            _ => throw new NotSupportedException($"Unsupported event '{document.eventType}' version {document.eventVersion}.")
        };

        return new StoredOrderEvent(
            document.sequenceNumber,
            document.aggregateId,
            document.eventType,
            document.eventVersion,
            document.occurredAtUtc,
            document.correlationId,
            document.causationId,
            payload);
    }
}