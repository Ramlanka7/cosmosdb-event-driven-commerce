using CosmosBootstrapper.Configuration;
using CosmosBootstrapper.Infrastructure;
using CosmosBootstrapper.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http;

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