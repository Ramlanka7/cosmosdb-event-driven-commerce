namespace Commerce.Eventing.Abstractions;

public interface IOrderEventPayload
{
    string EventType { get; }

    int EventVersion { get; }

    string OrderId { get; }

    string UserId { get; }

    DateTime OccurredAtUtc { get; }
}