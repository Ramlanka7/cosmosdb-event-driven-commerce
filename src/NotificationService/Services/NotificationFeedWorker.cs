using System.Text.Json;
using Commerce.Eventing.Contracts;
using Commerce.Eventing.Infrastructure;
using Commerce.Eventing.ReadModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using NotificationService.Configuration;

namespace NotificationService.Services;

internal sealed class NotificationFeedWorker(
    CosmosClient cosmosClient,
    IOptions<CosmosDbOptions> options,
    JsonSerializerOptions serializerOptions,
    INotificationStore notificationStore,
    ILogger<NotificationFeedWorker> logger) : BackgroundService
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
        logger.LogInformation("Started notification processor {ProcessorName}.", _options.ProcessorName);

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
            NotificationDocument notification = NotificationFactory.Create(envelope);
            bool created = await notificationStore.RecordAsync(notification, cancellationToken);

            if (created)
            {
                logger.LogInformation(
                    "Dispatched {NotificationType} notification for order {OrderId} and user {UserId}.",
                    notification.notificationType,
                    notification.orderId,
                    notification.userId);
            }
        }
    }
}