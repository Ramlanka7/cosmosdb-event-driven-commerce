using System.Net;
using ChangeFeedProcessor.Configuration;
using Commerce.Eventing.Contracts;
using Commerce.Eventing.Infrastructure;
using Commerce.Eventing.ReadModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace ChangeFeedProcessor.Services;

internal interface IChangeFeedFailureStore
{
    Task RecordAsync(
        CosmosEventDocument sourceDocument,
        OrderEventEnvelope? envelope,
        string handlerName,
        Exception exception,
        CancellationToken cancellationToken);
}

internal sealed class CosmosChangeFeedFailureStore(
    CosmosClient cosmosClient,
    IOptions<CosmosDbOptions> options,
    ILogger<CosmosChangeFeedFailureStore> logger) : IChangeFeedFailureStore
{
    private const string DocumentType = "change-feed-failure";
    private const int MaxErrorMessageLength = 2048;
    private const int MaxStackTraceLength = 8192;
    private readonly CosmosDbOptions _options = options.Value;

    public async Task RecordAsync(
        CosmosEventDocument sourceDocument,
        OrderEventEnvelope? envelope,
        string handlerName,
        Exception exception,
        CancellationToken cancellationToken)
    {
        DateTime failureTimeUtc = DateTime.UtcNow;
        string documentId = $"{_options.ProcessorName}:{handlerName}:{sourceDocument.aggregateId}:{sourceDocument.sequenceNumber:D10}";
        string? userId = envelope?.Payload.UserId ?? TryReadUserId(sourceDocument);

        ChangeFeedFailureDocument? current = await TryReadAsync(documentId, sourceDocument.aggregateId, cancellationToken);

        ChangeFeedFailureDocument updated = current is null
            ? new ChangeFeedFailureDocument
            {
                id = documentId,
                documentType = DocumentType,
                processorName = _options.ProcessorName,
                handlerName = handlerName,
                aggregateId = sourceDocument.aggregateId,
                sourceEventDocumentId = sourceDocument.id,
                userId = userId,
                eventType = sourceDocument.eventType,
                eventVersion = sourceDocument.eventVersion,
                sequenceNumber = sourceDocument.sequenceNumber,
                occurredAtUtc = sourceDocument.occurredAtUtc,
                firstFailedAtUtc = failureTimeUtc,
                lastFailedAtUtc = failureTimeUtc,
                failureCount = 1,
                lastErrorType = exception.GetType().FullName ?? exception.GetType().Name,
                lastErrorMessage = TruncateRequired(exception.Message, MaxErrorMessageLength),
                lastErrorStackTrace = Truncate(exception.StackTrace, MaxStackTraceLength),
                correlationId = sourceDocument.correlationId,
                causationId = sourceDocument.causationId,
                lastAttemptedByInstanceName = _options.InstanceName
            }
            : current with
            {
                userId = current.userId ?? userId,
                lastFailedAtUtc = failureTimeUtc,
                failureCount = current.failureCount + 1,
                lastErrorType = exception.GetType().FullName ?? exception.GetType().Name,
                lastErrorMessage = TruncateRequired(exception.Message, MaxErrorMessageLength),
                lastErrorStackTrace = Truncate(exception.StackTrace, MaxStackTraceLength),
                lastAttemptedByInstanceName = _options.InstanceName
            };

        ItemResponse<ChangeFeedFailureDocument> response = await GetContainer()
            .UpsertItemAsync(updated, new PartitionKey(updated.aggregateId), cancellationToken: cancellationToken);

        logger.LogWarning(
            "Recorded change feed failure for handler {HandlerName} on aggregate {AggregateId} sequence {SequenceNumber} with {RequestCharge:F2} RU.",
            handlerName,
            updated.aggregateId,
            updated.sequenceNumber,
            response.RequestCharge);
    }

    private async Task<ChangeFeedFailureDocument?> TryReadAsync(string documentId, string aggregateId, CancellationToken cancellationToken)
    {
        try
        {
            ItemResponse<ChangeFeedFailureDocument> response = await GetContainer()
                .ReadItemAsync<ChangeFeedFailureDocument>(documentId, new PartitionKey(aggregateId), cancellationToken: cancellationToken);

            return response.Resource;
        }
        catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private Container GetContainer() => cosmosClient.GetContainer(_options.DatabaseName, _options.FailuresContainerName);

    private static string? TryReadUserId(CosmosEventDocument sourceDocument)
    {
        JToken? userIdToken = sourceDocument.payload["userId"];
        if (userIdToken is not null && userIdToken.Type == JTokenType.String)
        {
            return userIdToken.Value<string>();
        }

        return null;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }

    private static string TruncateRequired(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}