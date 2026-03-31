using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CosmosBootstrapper.Services;

internal sealed class CosmosBootstrapperHostedService(
    ICosmosProvisioningService provisioningService,
    IHostApplicationLifetime applicationLifetime,
    ILogger<CosmosBootstrapperHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
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
        finally
        {
            applicationLifetime.StopApplication();
        }
    }
}