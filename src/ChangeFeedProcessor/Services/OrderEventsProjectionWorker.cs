using System.Text.Json;
using ChangeFeedProcessor.Configuration;
using Commerce.Eventing.Contracts;
using Commerce.Eventing.Infrastructure;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace ChangeFeedProcessor.Services;

internal sealed class OrderEventsProjectionWorker(
    CosmosClient cosmosClient,
    IOptions<CosmosDbOptions> options,
    JsonSerializerOptions serializerOptions,
    IChangeFeedFailureStore failureStore,
    IOrderEventProjectionDispatcher dispatcher,
    ILogger<OrderEventsProjectionWorker> logger) : BackgroundService
{
    private readonly CosmosDbOptions _options = options.Value;
    private Microsoft.Azure.Cosmos.ChangeFeedProcessor? _processor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Container monitoredContainer = cosmosClient.GetContainer(_options.DatabaseName, _options.OrderEventsContainerName);
        Container leaseContainer = cosmosClient.GetContainer(_options.DatabaseName, _options.LeasesContainerName);

        _processor = monitoredContainer
            .GetChangeFeedProcessorBuilder<CosmosEventDocument>(_options.ProcessorName, HandleChangesAsync)
            .WithInstanceName(_options.InstanceName)
            .WithLeaseContainer(leaseContainer)
            .Build();

        await _processor.StartAsync();
        logger.LogInformation(
            "Started change feed processor {ProcessorName} using lease container {LeaseContainerName}.",
            _options.ProcessorName,
            _options.LeasesContainerName);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (_processor is not null)
            {
                await _processor.StopAsync();
                logger.LogInformation("Stopped change feed processor {ProcessorName}.", _options.ProcessorName);
            }
        }
    }

    private async Task HandleChangesAsync(IReadOnlyCollection<CosmosEventDocument> documents, CancellationToken cancellationToken)
    {
        foreach (CosmosEventDocument document in documents
                     .Where(item => string.Equals(item.documentType, "event", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(item => item.sequenceNumber)
                     .ThenBy(item => item.id, StringComparer.Ordinal))
        {
            OrderEventEnvelope envelope;

            try
            {
                envelope = OrderEventDocumentDeserializer.Deserialize(document, serializerOptions);
            }
            catch (Exception exception)
            {
                await failureStore.RecordAsync(document, null, "event-deserialization", exception, cancellationToken);

                logger.LogError(
                    exception,
                    "Failed to deserialize event document {DocumentId} for aggregate {AggregateId} at sequence {SequenceNumber}.",
                    document.id,
                    document.aggregateId,
                    document.sequenceNumber);

                continue;
            }

            try
            {
                await dispatcher.DispatchAsync(document, envelope, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Skipping event document {DocumentId} for aggregate {AggregateId} at sequence {SequenceNumber} after projection failure.",
                    document.id,
                    document.aggregateId,
                    document.sequenceNumber);
            }
        }
    }
}