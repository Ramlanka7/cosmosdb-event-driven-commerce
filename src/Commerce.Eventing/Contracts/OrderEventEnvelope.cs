using Commerce.Eventing.Abstractions;

namespace Commerce.Eventing.Contracts;

public sealed record OrderEventEnvelope(
    string AggregateId,
    int SequenceNumber,
    string EventType,
    int EventVersion,
    DateTime OccurredAtUtc,
    string? CorrelationId,
    string? CausationId,
    IOrderEventPayload Payload);