namespace OrderService.Domain;

internal sealed record OrderConfirmed(string OrderId, string UserId, DateTime OccurredAtUtc) : IOrderEvent
{
    public string EventType => OrderEventTypes.OrderConfirmed;

    public int EventVersion => 1;
}