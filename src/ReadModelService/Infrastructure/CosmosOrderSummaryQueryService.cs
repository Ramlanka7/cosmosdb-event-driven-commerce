using Commerce.Eventing.ReadModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using ReadModelService.Configuration;

namespace ReadModelService.Infrastructure;

internal interface IOrderSummaryQueryService
{
    Task<IReadOnlyCollection<OrderSummaryDocument>> ListOrdersAsync(string userId, CancellationToken cancellationToken);

    Task<OrderSummaryDocument?> GetOrderAsync(string userId, string orderId, CancellationToken cancellationToken);
}

internal sealed class CosmosOrderSummaryQueryService(
    CosmosClient cosmosClient,
    IOptions<CosmosDbOptions> options,
    ILogger<CosmosOrderSummaryQueryService> logger) : IOrderSummaryQueryService
{
    private readonly CosmosDbOptions _options = options.Value;

    public async Task<IReadOnlyCollection<OrderSummaryDocument>> ListOrdersAsync(string userId, CancellationToken cancellationToken)
    {
        QueryDefinition query = new QueryDefinition(
            "SELECT * FROM c WHERE c.documentType = @documentType AND c.userId = @userId ORDER BY c.lastUpdatedAtUtc DESC")
            .WithParameter("@documentType", "order-summary")
            .WithParameter("@userId", userId);

        QueryRequestOptions requestOptions = new()
        {
            PartitionKey = new PartitionKey(userId),
            MaxConcurrency = 1
        };

        using FeedIterator<OrderSummaryDocument> iterator = GetContainer()
            .GetItemQueryIterator<OrderSummaryDocument>(query, requestOptions: requestOptions);

        List<OrderSummaryDocument> results = [];
        double requestCharge = 0;

        while (iterator.HasMoreResults)
        {
            FeedResponse<OrderSummaryDocument> response = await iterator.ReadNextAsync(cancellationToken);
            requestCharge += response.RequestCharge;
            results.AddRange(response);
        }

        logger.LogInformation(
            "Loaded {OrderCount} order summaries for user {UserId} with {RequestCharge:F2} RU.",
            results.Count,
            userId,
            requestCharge);

        return results;
    }

    public async Task<OrderSummaryDocument?> GetOrderAsync(string userId, string orderId, CancellationToken cancellationToken)
    {
        try
        {
            ItemResponse<OrderSummaryDocument> response = await GetContainer()
                .ReadItemAsync<OrderSummaryDocument>(orderId, new PartitionKey(userId), cancellationToken: cancellationToken);

            return response.Resource;
        }
        catch (CosmosException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private Container GetContainer() => cosmosClient.GetContainer(_options.DatabaseName, _options.OrdersReadContainerName);
}