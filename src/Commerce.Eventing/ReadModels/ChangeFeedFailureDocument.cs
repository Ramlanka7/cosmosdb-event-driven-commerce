namespace Commerce.Eventing.ReadModels;

public sealed record class ChangeFeedFailureDocument
{
    public required string id { get; init; }

    public required string documentType { get; init; }

    public required string processorName { get; init; }

    public required string handlerName { get; init; }

    public required string aggregateId { get; init; }

    public required string sourceEventDocumentId { get; init; }

    public string? userId { get; init; }

    public required string eventType { get; init; }

    public required int eventVersion { get; init; }

    public required int sequenceNumber { get; init; }

    public required DateTime occurredAtUtc { get; init; }

    public required DateTime firstFailedAtUtc { get; init; }

    public required DateTime lastFailedAtUtc { get; init; }

    public required int failureCount { get; init; }

    public required string lastErrorType { get; init; }

    public required string lastErrorMessage { get; init; }

    public string? lastErrorStackTrace { get; init; }

    public string? correlationId { get; init; }

    public string? causationId { get; init; }

    public string? lastAttemptedByInstanceName { get; init; }
}