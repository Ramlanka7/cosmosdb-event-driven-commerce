namespace OrderService.Domain;

internal sealed record OrderCreated(
    string OrderId,
    string UserId,
    IReadOnlyCollection<OrderItem> Items,
    DateTime OccurredAtUtc) : IOrderEvent
{
    public string EventType => OrderEventTypes.OrderCreated;

    public int EventVersion => 1;
}