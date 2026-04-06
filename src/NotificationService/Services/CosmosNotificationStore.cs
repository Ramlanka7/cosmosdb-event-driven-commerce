using System.Net;
using Commerce.Eventing.ReadModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using NotificationService.Configuration;

namespace NotificationService.Services;

internal interface INotificationStore
{
    Task<bool> RecordAsync(NotificationDocument document, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<NotificationDocument>> ListAsync(string userId, CancellationToken cancellationToken);
}

internal sealed class CosmosNotificationStore(
    CosmosClient cosmosClient,
    IOptions<CosmosDbOptions> options,
    ILogger<CosmosNotificationStore> logger) : INotificationStore
{
    private readonly CosmosDbOptions _options = options.Value;

    public async Task<bool> RecordAsync(NotificationDocument document, CancellationToken cancellationToken)
    {
        try
        {
            ItemResponse<NotificationDocument> response = await GetContainer()
                .CreateItemAsync(document, new PartitionKey(document.userId), cancellationToken: cancellationToken);

            logger.LogInformation(
                "Recorded notification {NotificationId} for order {OrderId} with {RequestCharge:F2} RU.",
                response.Resource.id,
                response.Resource.orderId,
                response.RequestCharge);

            return true;
        }
        catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.Conflict)
        {
            return false;
        }
    }

    public async Task<IReadOnlyCollection<NotificationDocument>> ListAsync(string userId, CancellationToken cancellationToken)
    {
        QueryDefinition query = new QueryDefinition(
            "SELECT TOP @maxItems * FROM c WHERE c.documentType = @documentType AND c.userId = @userId ORDER BY c.occurredAtUtc DESC")
            .WithParameter("@documentType", "notification")
            .WithParameter("@userId", userId)
            .WithParameter("@maxItems", 100);

        QueryRequestOptions requestOptions = new()
        {
            PartitionKey = new PartitionKey(userId),
            MaxConcurrency = 1,
            MaxItemCount = 100
        };

        using FeedIterator<NotificationDocument> iterator = GetContainer()
            .GetItemQueryIterator<NotificationDocument>(query, requestOptions: requestOptions);

        List<NotificationDocument> results = [];

        while (iterator.HasMoreResults)
        {
            FeedResponse<NotificationDocument> response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results;
    }

    private Container GetContainer() => cosmosClient.GetContainer(_options.DatabaseName, _options.NotificationsContainerName);
}