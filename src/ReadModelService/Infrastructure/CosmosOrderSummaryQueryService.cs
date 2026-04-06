using Commerce.Eventing.ReadModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using ReadModelService.Configuration;

namespace ReadModelService.Infrastructure;

internal interface IOrderSummaryQueryService
{
    Task<OrderSummaryPage> ListOrdersAsync(OrderSummaryQuery query, CancellationToken cancellationToken);

    Task<OrderSummaryDocument?> GetOrderAsync(string userId, string orderId, CancellationToken cancellationToken);
}

internal sealed class CosmosOrderSummaryQueryService(
    CosmosClient cosmosClient,
    IOptions<CosmosDbOptions> options,
    ILogger<CosmosOrderSummaryQueryService> logger) : IOrderSummaryQueryService
{
    private readonly CosmosDbOptions _options = options.Value;

    public async Task<OrderSummaryPage> ListOrdersAsync(OrderSummaryQuery query, CancellationToken cancellationToken)
    {
        OrderSummaryQuerySpecification specification = OrderSummaryQuerySpecificationBuilder.Build(query);
        QueryDefinition queryDefinition = new(specification.QueryText);

        foreach ((string name, object value) in specification.Parameters)
        {
            queryDefinition.WithParameter(name, value);
        }

        QueryRequestOptions requestOptions = new()
        {
            PartitionKey = new PartitionKey(specification.UserId),
            MaxConcurrency = 1,
            MaxItemCount = specification.PageSize
        };

        using FeedIterator<OrderSummaryDocument> iterator = GetContainer()
            .GetItemQueryIterator<OrderSummaryDocument>(queryDefinition, specification.ContinuationToken, requestOptions);

        if (!iterator.HasMoreResults)
        {
            return new OrderSummaryPage([], null, specification.PageSize, 0);
        }

        FeedResponse<OrderSummaryDocument> response = await iterator.ReadNextAsync(cancellationToken);
        OrderSummaryDocument[] results = response.ToArray();

        logger.LogInformation(
            "Loaded {OrderCount} order summaries for user {UserId} with status filter {StatusFilter}, updatedAfter {UpdatedAfterUtc}, updatedBefore {UpdatedBeforeUtc}, page size {PageSize}, next token present {HasContinuationToken}, and {RequestCharge:F2} RU.",
            results.Length,
            query.UserId,
            query.Status ?? "(any)",
            query.UpdatedAfterUtc,
            query.UpdatedBeforeUtc,
            query.PageSize,
            !string.IsNullOrWhiteSpace(response.ContinuationToken),
            response.RequestCharge);

        return new OrderSummaryPage(results, response.ContinuationToken, specification.PageSize, response.RequestCharge);
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