using OrderService.Domain;

namespace OrderService.Infrastructure;

internal sealed record StoredOrderEvent(
    int SequenceNumber,
    string AggregateId,
    string EventType,
    int EventVersion,
    DateTime OccurredAtUtc,
    string? CorrelationId,
    string? CausationId,
    IOrderEvent Payload);