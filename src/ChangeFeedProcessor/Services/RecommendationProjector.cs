using System.Text.Json;
using ChangeFeedProcessor.Configuration;
using Commerce.Eventing.Contracts;
using Commerce.Eventing.Infrastructure;
using Commerce.Eventing.ReadModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace ChangeFeedProcessor.Services;

internal sealed class RecommendationProjector(
    CosmosClient cosmosClient,
    IOptions<CosmosDbOptions> options,
    JsonSerializerOptions serializerOptions,
    ILogger<RecommendationProjector> logger) : IOrderEventProjectionHandler
{
    private readonly CosmosDbOptions _options = options.Value;

    public string Name => "recommendation-projection";

    public async Task ProjectAsync(OrderEventEnvelope envelope, CancellationToken cancellationToken)
    {
        RecommendationDocument? current = await TryReadAsync(envelope.Payload.UserId, cancellationToken);

        if (current is null && ShouldRebuildFromHistory(envelope))
        {
            current = await RebuildFromHistoryAsync(envelope.Payload.UserId, cancellationToken);
        }

        RecommendationDocument updated = RecommendationProjectionApplier.Apply(current, envelope);

        if (ReferenceEquals(current, updated))
        {
            return;
        }

        ItemResponse<RecommendationDocument> response = await GetRecommendationsContainer()
            .UpsertItemAsync(updated, new PartitionKey(updated.userId), cancellationToken: cancellationToken);

        logger.LogInformation(
            "Projected {EventType} for user {UserId} into recommendations at sequence {SequenceNumber} with {SuggestionCount} suggestions using {RequestCharge:F2} RU.",
            envelope.EventType,
            updated.userId,
            envelope.SequenceNumber,
            updated.suggestedSkus.Count,
            response.RequestCharge);
    }

    private async Task<RecommendationDocument?> TryReadAsync(string userId, CancellationToken cancellationToken)
    {
        try
        {
            ItemResponse<RecommendationDocument> response = await GetRecommendationsContainer()
                .ReadItemAsync<RecommendationDocument>(userId, new PartitionKey(userId), cancellationToken: cancellationToken);

            return response.Resource;
        }
        catch (CosmosException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task<RecommendationDocument?> RebuildFromHistoryAsync(string userId, CancellationToken cancellationToken)
    {
        QueryDefinition query = new QueryDefinition(
            "SELECT * FROM c WHERE c.documentType = @documentType AND c.payload.userId = @userId")
            .WithParameter("@documentType", "event")
            .WithParameter("@userId", userId);

        QueryRequestOptions requestOptions = new()
        {
            MaxConcurrency = 1
        };

        using FeedIterator<CosmosEventDocument> iterator = GetOrderEventsContainer()
            .GetItemQueryIterator<CosmosEventDocument>(query, requestOptions: requestOptions);

        List<CosmosEventDocument> events = [];

        while (iterator.HasMoreResults)
        {
            FeedResponse<CosmosEventDocument> response = await iterator.ReadNextAsync(cancellationToken);
            events.AddRange(response);
        }

        RecommendationDocument? current = null;

        foreach (CosmosEventDocument document in events
                     .OrderBy(item => item.occurredAtUtc)
                     .ThenBy(item => item.aggregateId, StringComparer.Ordinal)
                     .ThenBy(item => item.sequenceNumber))
        {
            OrderEventEnvelope envelope = OrderEventDocumentDeserializer.Deserialize(document, serializerOptions);
            current = RecommendationProjectionApplier.Apply(current, envelope);
        }

        return current;
    }

    private bool ShouldRebuildFromHistory(OrderEventEnvelope envelope) => envelope.SequenceNumber > 1;

    private Container GetOrderEventsContainer() => cosmosClient.GetContainer(_options.DatabaseName, _options.OrderEventsContainerName);

    private Container GetRecommendationsContainer() => cosmosClient.GetContainer(_options.DatabaseName, _options.RecommendationsContainerName);
}