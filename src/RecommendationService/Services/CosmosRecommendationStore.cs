using System.Net;
using Commerce.Eventing.ReadModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using RecommendationService.Configuration;

namespace RecommendationService.Services;

internal interface IRecommendationStore
{
    Task<RecommendationDocument?> GetAsync(string userId, CancellationToken cancellationToken);

    Task UpsertAsync(RecommendationDocument document, CancellationToken cancellationToken);
}

internal sealed class CosmosRecommendationStore(
    CosmosClient cosmosClient,
    IOptions<CosmosDbOptions> options,
    ILogger<CosmosRecommendationStore> logger) : IRecommendationStore
{
    private readonly CosmosDbOptions _options = options.Value;

    public async Task<RecommendationDocument?> GetAsync(string userId, CancellationToken cancellationToken)
    {
        try
        {
            ItemResponse<RecommendationDocument> response = await GetContainer()
                .ReadItemAsync<RecommendationDocument>(userId, new PartitionKey(userId), cancellationToken: cancellationToken);

            return response.Resource;
        }
        catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task UpsertAsync(RecommendationDocument document, CancellationToken cancellationToken)
    {
        ItemResponse<RecommendationDocument> response = await GetContainer()
            .UpsertItemAsync(document, new PartitionKey(document.userId), cancellationToken: cancellationToken);

        logger.LogInformation(
            "Upserted recommendation profile for user {UserId} with {SuggestionCount} suggestions at sequence {SequenceNumber} using {RequestCharge:F2} RU.",
            document.userId,
            document.suggestedSkus.Count,
            document.lastProcessedSequenceNumber,
            response.RequestCharge);
    }

    private Container GetContainer() => cosmosClient.GetContainer(_options.DatabaseName, _options.RecommendationsContainerName);
}