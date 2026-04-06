using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CosmosBootstrapper.Services;

internal sealed class CosmosBootstrapperHostedService(
    ICosmosProvisioningService provisioningService,
    IHostApplicationLifetime applicationLifetime,
    IConfiguration configuration,
    ILogger<CosmosBootstrapperHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        bool continuousMode = configuration.GetValue<bool>("Bootstrapper:ContinuousMode");
        int intervalSeconds = Math.Max(5, configuration.GetValue<int?>("Bootstrapper:ProvisioningIntervalSeconds") ?? 30);

        try
        {
            do
            {
                try
                {
                    await provisioningService.ProvisionAsync(stoppingToken);
                    Environment.ExitCode = 0;
                }
                catch (CosmosException exception)
                {
                    Environment.ExitCode = 1;
                    logger.LogError(exception, "Cosmos DB request failed with status {StatusCode}. Diagnostics: {Diagnostics}", exception.StatusCode, exception.Diagnostics);
                }
                catch (Exception exception)
                {
                    Environment.ExitCode = 1;
                    logger.LogError(exception, "Unexpected failure while provisioning Cosmos DB resources.");
                }

                if (!continuousMode)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
            while (!stoppingToken.IsCancellationRequested);
        }
        finally
        {
            if (!continuousMode)
            {
                applicationLifetime.StopApplication();
            }
        }
    }
}