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

        if (ShouldAllowInsecureCertificate(options.Endpoint))
        {
            clientOptions.ConnectionMode = ConnectionMode.Gateway;
            clientOptions.HttpClientFactory = () => new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
        }

        return new CosmosClient(options.Endpoint, options.Key, clientOptions);
    }

    private static bool ShouldAllowInsecureCertificate(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? endpointUri))
        {
            return false;
        }

        return endpointUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
               || endpointUri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
             || endpointUri.Host.Equals("host.docker.internal", StringComparison.OrdinalIgnoreCase)
             || endpointUri.Host.Equals("cosmos-emulator", StringComparison.OrdinalIgnoreCase);
    }
}