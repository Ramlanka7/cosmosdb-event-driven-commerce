using Commerce.Eventing.Contracts;

namespace Commerce.Eventing.ReadModels;

public sealed record class OrderSummaryDocument
{
    public required string id { get; init; }

    public required string documentType { get; init; }

    public required string orderId { get; init; }

    public required string userId { get; init; }

    public required string status { get; init; }

    public required IReadOnlyCollection<OrderItemData> items { get; init; }

    public required decimal totalAmount { get; init; }

    public required DateTime createdAtUtc { get; init; }

    public required DateTime lastUpdatedAtUtc { get; init; }

    public required int lastProcessedSequenceNumber { get; init; }

    public required int projectionVersion { get; init; }
}