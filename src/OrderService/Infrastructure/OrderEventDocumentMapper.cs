using System.Text.Json;
using OrderService.Domain;
using Newtonsoft.Json.Linq;

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
        string payloadJson = System.Text.Json.JsonSerializer.Serialize((object)orderEvent, orderEvent.GetType(), serializerOptions);

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
            payload = JObject.Parse(payloadJson)
        };
    }

    public StoredOrderEvent ToStoredEvent(CosmosEventDocument document)
    {
        string payloadJson = document.payload.ToString(Newtonsoft.Json.Formatting.None);

        IOrderEvent payload = (document.eventType, document.eventVersion) switch
        {
            (OrderEventTypes.OrderCreated, 1) => System.Text.Json.JsonSerializer.Deserialize<OrderCreated>(payloadJson, serializerOptions)
                ?? throw new InvalidOperationException("Unable to deserialize order-created event payload."),
            (OrderEventTypes.OrderConfirmed, 1) => System.Text.Json.JsonSerializer.Deserialize<OrderConfirmed>(payloadJson, serializerOptions)
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