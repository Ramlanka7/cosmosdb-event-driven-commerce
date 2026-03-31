using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using RecommendationService.Configuration;

namespace RecommendationService.Infrastructure;

internal static class CosmosClientFactory
{
    public static CosmosClient Create(IServiceProvider serviceProvider)
    {
        CosmosDbOptions options = serviceProvider
            .GetRequiredService<IOptions<CosmosDbOptions>>()
            .Value;

        CosmosClientOptions clientOptions = new()
        {
            ApplicationName = "cosmosdb-event-driven-commerce-recommendation-service",
            ConnectionMode = ConnectionMode.Direct,
            MaxRetryAttemptsOnRateLimitedRequests = 9,
            MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(30)
        };

        if (options.PreferredRegions.Count > 0)
        {
            clientOptions.ApplicationPreferredRegions = options.PreferredRegions;
        }

        return new CosmosClient(options.Endpoint, options.Key, clientOptions);
    }
}