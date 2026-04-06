namespace OrderService.Domain;

internal interface IOrderEvent
{
    string EventType { get; }

    int EventVersion { get; }

    DateTime OccurredAtUtc { get; }
}