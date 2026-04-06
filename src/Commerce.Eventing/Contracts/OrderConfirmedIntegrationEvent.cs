using Commerce.Eventing.Abstractions;

namespace Commerce.Eventing.Contracts;

public sealed record OrderConfirmedIntegrationEvent(
    string OrderId,
    string UserId,
    DateTime OccurredAtUtc) : IOrderEventPayload
{
    public string EventType => OrderEventTypes.OrderConfirmed;

    public int EventVersion => 1;
}