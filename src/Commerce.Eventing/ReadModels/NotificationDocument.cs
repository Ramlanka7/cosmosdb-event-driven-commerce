namespace Commerce.Eventing.ReadModels;

public sealed class NotificationDocument
{
    public required string id { get; init; }

    public required string documentType { get; init; }

    public required string userId { get; init; }

    public required string orderId { get; init; }

    public required string notificationType { get; init; }

    public required string message { get; init; }

    public required DateTime occurredAtUtc { get; init; }

    public string? correlationId { get; init; }

    public string? causationId { get; init; }

    public required int sourceEventSequenceNumber { get; init; }

    public required int projectionVersion { get; init; }
}