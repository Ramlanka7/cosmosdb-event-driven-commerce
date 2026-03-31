using System.Text.Json;
using Commerce.Eventing.Contracts;
using Commerce.Eventing.Infrastructure;
using Commerce.Eventing.ReadModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using RecommendationService.Configuration;

namespace RecommendationService.Services;

internal sealed class RecommendationFeedWorker(
    CosmosClient cosmosClient,
    IOptions<CosmosDbOptions> options,
    JsonSerializerOptions serializerOptions,
    IRecommendationStore recommendationStore,
    ILogger<RecommendationFeedWorker> logger) : BackgroundService
{
    private readonly CosmosDbOptions _options = options.Value;
    private ChangeFeedProcessor? _processor;

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
        logger.LogInformation("Started recommendation processor {ProcessorName}.", _options.ProcessorName);

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
            }
        }
    }

    private async Task HandleChangesAsync(IReadOnlyCollection<CosmosEventDocument> documents, CancellationToken cancellationToken)
    {
        foreach (CosmosEventDocument document in documents
                     .Where(item => string.Equals(item.documentType, "event", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(item => item.sequenceNumber))
        {
            OrderEventEnvelope envelope = OrderEventDocumentDeserializer.Deserialize(document, serializerOptions);
            RecommendationDocument? current = await recommendationStore.GetAsync(envelope.Payload.UserId, cancellationToken);
            RecommendationDocument updated = RecommendationProjectionApplier.Apply(current, envelope);
            await recommendationStore.UpsertAsync(updated, cancellationToken);
        }
    }
}