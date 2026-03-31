using System.Net;
using CosmosBootstrapper.Configuration;
using CosmosBootstrapper.Domain;
using CosmosBootstrapper.Infrastructure;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CosmosBootstrapper.Services;

internal interface ICosmosProvisioningService
{
    Task ProvisionAsync(CancellationToken cancellationToken);
}

internal sealed class CosmosProvisioningService(
    CosmosClient cosmosClient,
    IOptions<CosmosDbOptions> options,
    ICosmosContainerCatalog containerCatalog,
    ILogger<CosmosProvisioningService> logger) : ICosmosProvisioningService
{
    private readonly CosmosDbOptions _options = options.Value;

    public async Task ProvisionAsync(CancellationToken cancellationToken)
    {
        await EnsureAccountReachableAsync();

        if (_options.ResetDatabaseOnProvision)
        {
            await ResetDatabaseIfExistsAsync(cancellationToken);
        }

        Database database = await EnsureDatabaseAsync(cancellationToken);
        await EnsureSharedThroughputAsync(database, cancellationToken);
        await EnsureContainersAsync(database, cancellationToken);

        if (_options.VerifyWrite)
        {
            await VerifyWriteAsync(database, cancellationToken);
        }

        logger.LogInformation("Provisioning completed for database {DatabaseName}.", _options.DatabaseName);
    }

    private async Task EnsureAccountReachableAsync()
    {
        await cosmosClient.ReadAccountAsync();
        logger.LogInformation("Connected to Cosmos DB account {AccountHost}.", new Uri(_options.Endpoint).Host);
    }

    private async Task ResetDatabaseIfExistsAsync(CancellationToken cancellationToken)
    {
        Database database = cosmosClient.GetDatabase(_options.DatabaseName);

        try
        {
            await database.DeleteAsync(cancellationToken: cancellationToken);
            logger.LogInformation("Deleted existing database {DatabaseName} so it can be recreated with shared throughput.", _options.DatabaseName);
        }
        catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogInformation("Database {DatabaseName} does not exist yet. No reset needed.", _options.DatabaseName);
        }
    }

    private async Task<Database> EnsureDatabaseAsync(CancellationToken cancellationToken)
    {
        DatabaseResponse response = _options.DatabaseManualThroughput is > 0
            ? await cosmosClient.CreateDatabaseIfNotExistsAsync(
                _options.DatabaseName,
                ThroughputProperties.CreateManualThroughput(_options.DatabaseManualThroughput.Value),
                cancellationToken: cancellationToken)
            : await cosmosClient.CreateDatabaseIfNotExistsAsync(_options.DatabaseName, cancellationToken: cancellationToken);

        logger.LogInformation("Database ready: {DatabaseName}", response.Database.Id);
        return response.Database;
    }

    private async Task EnsureSharedThroughputAsync(Database database, CancellationToken cancellationToken)
    {
        int? throughput = await database.ReadThroughputAsync(cancellationToken: cancellationToken);

        if (throughput is null)
        {
            throw new InvalidOperationException($"Database {_options.DatabaseName} exists without shared throughput. Reset it or enable shared database throughput before creating practice containers.");
        }

        logger.LogInformation("Database throughput is shared at {Throughput} RU/s.", throughput.Value);
    }

    private async Task EnsureContainersAsync(Database database, CancellationToken cancellationToken)
    {
        foreach (ContainerDefinition definition in containerCatalog.GetRequiredContainers())
        {
            ContainerProperties properties = new(definition.Name, definition.PartitionKeyPath);
            ContainerResponse response = await database.CreateContainerIfNotExistsAsync(properties, cancellationToken: cancellationToken);
            logger.LogInformation("Container ready: {ContainerName} with partition key {PartitionKeyPath}", response.Container.Id, definition.PartitionKeyPath);
        }
    }

    private async Task VerifyWriteAsync(Database database, CancellationToken cancellationToken)
    {
        const string aggregateId = "bootstrap-order";
        Container orderEventsContainer = database.GetContainer("order-events");
        BootstrapVerificationEventDocument orderCreated = new()
        {
            id = $"{aggregateId}:0000000001",
            documentType = "event",
            aggregateId = aggregateId,
            eventType = "order-created",
            eventVersion = 1,
            sequenceNumber = 1,
            occurredAtUtc = DateTime.UtcNow,
            schemaVersion = 1,
            correlationId = "bootstrap-provisioning",
            causationId = "bootstrap-provisioning",
            payload = new BootstrapVerificationOrderCreated
            {
                orderId = aggregateId,
                userId = "bootstrap-user",
                occurredAtUtc = DateTime.UtcNow,
                items =
                [
                    new BootstrapVerificationOrderItem
                    {
                        sku = "bootstrap-sku",
                        quantity = 1,
                        unitPrice = 9.99m
                    }
                ]
            }
        };

        await orderEventsContainer.UpsertItemAsync(orderCreated, new PartitionKey(orderCreated.aggregateId), cancellationToken: cancellationToken);
        ItemResponse<BootstrapVerificationEventDocument> response = await orderEventsContainer.ReadItemAsync<BootstrapVerificationEventDocument>(orderCreated.id, new PartitionKey(orderCreated.aggregateId), cancellationToken: cancellationToken);

        logger.LogInformation(
            "Write verification succeeded for event stream {AggregateId} at sequence {SequenceNumber} with request charge {RequestCharge:F2} RU.",
            response.Resource.aggregateId,
            response.Resource.sequenceNumber,
            response.RequestCharge);
    }
}