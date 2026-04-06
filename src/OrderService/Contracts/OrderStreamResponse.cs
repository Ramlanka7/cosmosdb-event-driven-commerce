using OrderService.Domain;

namespace OrderService.Contracts;

internal sealed record OrderStreamResponse(string OrderId, int Version, IReadOnlyCollection<OrderEventResponse> Events);

internal sealed record OrderEventResponse(
    int SequenceNumber,
    string EventType,
    int EventVersion,
    DateTime OccurredAtUtc,
    string? CorrelationId,
    string? CausationId,
    IOrderEvent Payload);