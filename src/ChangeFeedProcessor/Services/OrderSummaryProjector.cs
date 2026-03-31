using System.Text.Json;
using ChangeFeedProcessor.Configuration;
using Commerce.Eventing.Contracts;
using Commerce.Eventing.Infrastructure;
using Commerce.Eventing.ReadModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace ChangeFeedProcessor.Services;

internal sealed class OrderSummaryProjector(
    CosmosClient cosmosClient,
    IOptions<CosmosDbOptions> options,
    JsonSerializerOptions serializerOptions,
    ILogger<OrderSummaryProjector> logger) : IOrderSummaryProjector
{
    private readonly CosmosDbOptions _options = options.Value;

    public async Task ProjectAsync(OrderEventEnvelope envelope, CancellationToken cancellationToken)
    {
        OrderSummaryDocument? current = await TryReadAsync(envelope.Payload.UserId, envelope.AggregateId, cancellationToken);

        if (current is null && envelope.Payload is OrderConfirmedIntegrationEvent)
        {
            current = await RebuildFromHistoryAsync(envelope.AggregateId, cancellationToken);
        }

        if (current is not null && current.lastProcessedSequenceNumber >= envelope.SequenceNumber)
        {
            return;
        }

        OrderSummaryDocument updated = OrderSummaryProjectionApplier.Apply(current, envelope);
        ItemResponse<OrderSummaryDocument> response = await GetOrdersReadContainer()
            .UpsertItemAsync(updated, new PartitionKey(updated.userId), cancellationToken: cancellationToken);

        logger.LogInformation(
            "Projected {EventType} for order {OrderId} into orders-read with status {Status} at sequence {SequenceNumber} using {RequestCharge:F2} RU.",
            envelope.EventType,
            updated.orderId,
            updated.status,
            updated.lastProcessedSequenceNumber,
            response.RequestCharge);
    }

    private async Task<OrderSummaryDocument?> TryReadAsync(string userId, string orderId, CancellationToken cancellationToken)
    {
        try
        {
            ItemResponse<OrderSummaryDocument> response = await GetOrdersReadContainer()
                .ReadItemAsync<OrderSummaryDocument>(orderId, new PartitionKey(userId), cancellationToken: cancellationToken);

            return response.Resource;
        }
        catch (CosmosException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task<OrderSummaryDocument?> RebuildFromHistoryAsync(string aggregateId, CancellationToken cancellationToken)
    {
        QueryDefinition query = new QueryDefinition(
            "SELECT * FROM c WHERE c.documentType = @documentType AND c.aggregateId = @aggregateId ORDER BY c.sequenceNumber ASC")
            .WithParameter("@documentType", "event")
            .WithParameter("@aggregateId", aggregateId);

        QueryRequestOptions requestOptions = new()
        {
            PartitionKey = new PartitionKey(aggregateId),
            MaxConcurrency = 1
        };

        using FeedIterator<CosmosEventDocument> iterator = GetOrderEventsContainer()
            .GetItemQueryIterator<CosmosEventDocument>(query, requestOptions: requestOptions);

        OrderSummaryDocument? current = null;

        while (iterator.HasMoreResults)
        {
            FeedResponse<CosmosEventDocument> response = await iterator.ReadNextAsync(cancellationToken);

            foreach (CosmosEventDocument document in response.OrderBy(item => item.sequenceNumber))
            {
                OrderEventEnvelope envelope = OrderEventDocumentDeserializer.Deserialize(document, serializerOptions);
                current = OrderSummaryProjectionApplier.Apply(current, envelope);
            }
        }

        return current;
    }

    private Container GetOrderEventsContainer() => cosmosClient.GetContainer(_options.DatabaseName, _options.OrderEventsContainerName);

    private Container GetOrdersReadContainer() => cosmosClient.GetContainer(_options.DatabaseName, _options.OrdersReadContainerName);
}