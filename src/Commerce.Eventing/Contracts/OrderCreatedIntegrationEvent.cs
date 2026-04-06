using Commerce.Eventing.Abstractions;

namespace Commerce.Eventing.Contracts;

public sealed record OrderCreatedIntegrationEvent(
    string OrderId,
    string UserId,
    IReadOnlyCollection<OrderItemData> Items,
    DateTime OccurredAtUtc) : IOrderEventPayload
{
    public string EventType => OrderEventTypes.OrderCreated;

    public int EventVersion => 1;
}