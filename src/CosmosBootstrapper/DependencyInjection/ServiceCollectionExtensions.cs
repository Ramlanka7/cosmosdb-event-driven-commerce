using CosmosBootstrapper.Configuration;
using CosmosBootstrapper.Infrastructure;
using CosmosBootstrapper.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CosmosBootstrapper.DependencyInjection;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCosmosBootstrapper(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            });
        });

        services
            .AddOptions<CosmosDbOptions>()
            .Bind(configuration.GetSection(CosmosDbOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<CosmosDbOptions>, CosmosDbOptionsValidator>();
        services.AddSingleton<ICosmosContainerCatalog, CosmosContainerCatalog>();
        services.AddSingleton<ICosmosProvisioningService, CosmosProvisioningService>();
        services.AddHostedService<CosmosBootstrapperHostedService>();
        services.AddSingleton(CreateCosmosClient);

        return services;
    }

    private static CosmosClient CreateCosmosClient(IServiceProvider serviceProvider)
    {
        CosmosDbOptions options = serviceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<CosmosDbOptions>>()
            .Value;

        CosmosClientOptions clientOptions = new()
        {
            ApplicationName = "cosmosdb-event-driven-commerce-bootstrapper",
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