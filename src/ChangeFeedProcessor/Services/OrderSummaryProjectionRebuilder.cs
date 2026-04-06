using System.Text.Json;
using ChangeFeedProcessor.Configuration;
using Commerce.Eventing.Contracts;
using Commerce.Eventing.Infrastructure;
using Commerce.Eventing.ReadModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace ChangeFeedProcessor.Services;

internal interface IOrderSummaryProjectionRebuilder
{
    Task<OrderSummaryDocument?> RebuildAsync(string aggregateId, CancellationToken cancellationToken);
}

internal sealed class CosmosOrderSummaryProjectionRebuilder(
    CosmosClient cosmosClient,
    IOptions<CosmosDbOptions> options,
    JsonSerializerOptions serializerOptions,
    ILogger<CosmosOrderSummaryProjectionRebuilder> logger) : IOrderSummaryProjectionRebuilder
{
    private readonly CosmosDbOptions _options = options.Value;

    public async Task<OrderSummaryDocument?> RebuildAsync(string aggregateId, CancellationToken cancellationToken)
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
        double requestCharge = 0;

        while (iterator.HasMoreResults)
        {
            FeedResponse<CosmosEventDocument> response = await iterator.ReadNextAsync(cancellationToken);
            requestCharge += response.RequestCharge;

            foreach (CosmosEventDocument document in response.OrderBy(item => item.sequenceNumber))
            {
                OrderEventEnvelope envelope = OrderEventDocumentDeserializer.Deserialize(document, serializerOptions);
                current = OrderSummaryProjectionApplier.Apply(current, envelope);
            }
        }

        logger.LogInformation(
            "Rebuilt order summary projection for aggregate {AggregateId}. Projection found: {ProjectionFound}. Request charge: {RequestCharge:F2} RU.",
            aggregateId,
            current is not null,
            requestCharge);

        return current;
    }

    private Container GetOrderEventsContainer() => cosmosClient.GetContainer(_options.DatabaseName, _options.OrderEventsContainerName);
}