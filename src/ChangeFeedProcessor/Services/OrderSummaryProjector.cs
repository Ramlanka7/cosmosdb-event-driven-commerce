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
    IOrderSummaryProjectionRebuilder projectionRebuilder,
    ILogger<OrderSummaryProjector> logger) : IOrderEventProjectionHandler
{
    private readonly CosmosDbOptions _options = options.Value;

    public string Name => "order-summary-projection";

    public async Task ProjectAsync(OrderEventEnvelope envelope, CancellationToken cancellationToken)
    {
        OrderSummaryDocument? current = await TryReadAsync(envelope.Payload.UserId, envelope.AggregateId, cancellationToken);

        if (current is null && envelope.SequenceNumber > 1)
        {
            current = await projectionRebuilder.RebuildAsync(envelope.AggregateId, cancellationToken);
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

    private Container GetOrdersReadContainer() => cosmosClient.GetContainer(_options.DatabaseName, _options.OrdersReadContainerName);
}