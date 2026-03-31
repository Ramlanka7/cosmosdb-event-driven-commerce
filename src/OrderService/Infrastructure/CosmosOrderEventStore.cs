using System.Diagnostics;
using System.Net;
using OrderService.Configuration;
using OrderService.Domain;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OrderService.Infrastructure;

internal interface IOrderEventStore
{
    Task<OrderStream> ReadStreamAsync(string aggregateId, CancellationToken cancellationToken);

    Task<StoredOrderEvent> AppendAsync(string aggregateId, int expectedVersion, IOrderEvent orderEvent, EventMetadata metadata, CancellationToken cancellationToken);
}

internal sealed class CosmosOrderEventStore(
    CosmosClient cosmosClient,
    IOptions<CosmosDbOptions> options,
    IOrderEventDocumentMapper documentMapper,
    ILogger<CosmosOrderEventStore> logger) : IOrderEventStore
{
    private readonly CosmosDbOptions _options = options.Value;

    public async Task<OrderStream> ReadStreamAsync(string aggregateId, CancellationToken cancellationToken)
    {
        Container container = GetContainer();
        QueryDefinition query = new QueryDefinition(
            "SELECT * FROM c WHERE c.documentType = @documentType AND c.aggregateId = @aggregateId ORDER BY c.sequenceNumber ASC")
            .WithParameter("@documentType", "event")
            .WithParameter("@aggregateId", aggregateId);

        QueryRequestOptions requestOptions = new()
        {
            PartitionKey = new PartitionKey(aggregateId),
            MaxConcurrency = 1
        };

        using FeedIterator<CosmosEventDocument> iterator = container.GetItemQueryIterator<CosmosEventDocument>(query, requestOptions: requestOptions);
        List<StoredOrderEvent> events = [];
        Stopwatch stopwatch = Stopwatch.StartNew();
        double requestCharge = 0;

        while (iterator.HasMoreResults)
        {
            FeedResponse<CosmosEventDocument> response = await iterator.ReadNextAsync(cancellationToken);
            requestCharge += response.RequestCharge;
            events.AddRange(response.Select(documentMapper.ToStoredEvent));
        }

        stopwatch.Stop();
        logger.LogInformation(
            "Loaded {EventCount} events for aggregate {AggregateId} in {ElapsedMilliseconds} ms with {RequestCharge:F2} RU.",
            events.Count,
            aggregateId,
            stopwatch.ElapsedMilliseconds,
            requestCharge);

        return new OrderStream(aggregateId, events.Count, events);
    }

    public async Task<StoredOrderEvent> AppendAsync(string aggregateId, int expectedVersion, IOrderEvent orderEvent, EventMetadata metadata, CancellationToken cancellationToken)
    {
        Container container = GetContainer();
        int nextSequenceNumber = expectedVersion + 1;
        CosmosEventDocument document = documentMapper.ToDocument(aggregateId, nextSequenceNumber, orderEvent, metadata);
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            ItemResponse<CosmosEventDocument> response = await container.CreateItemAsync(document, new PartitionKey(aggregateId), cancellationToken: cancellationToken);
            stopwatch.Stop();

            logger.LogInformation(
                "Appended event {EventType} v{EventVersion} to aggregate {AggregateId} at sequence {SequenceNumber} in {ElapsedMilliseconds} ms with {RequestCharge:F2} RU.",
                orderEvent.EventType,
                orderEvent.EventVersion,
                aggregateId,
                nextSequenceNumber,
                stopwatch.ElapsedMilliseconds,
                response.RequestCharge);

            return documentMapper.ToStoredEvent(response.Resource);
        }
        catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.Conflict)
        {
            stopwatch.Stop();
            throw new ConcurrentStreamWriteException(
                $"A concurrent write was detected for aggregate '{aggregateId}' at sequence {nextSequenceNumber}. Re-read the stream and retry.",
                exception);
        }
    }

    private Container GetContainer() => cosmosClient.GetContainer(_options.DatabaseName, _options.OrderEventsContainerName);
}