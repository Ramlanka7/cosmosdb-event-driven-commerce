namespace OrderService.Infrastructure;

internal sealed record OrderStream(string AggregateId, int Version, IReadOnlyList<StoredOrderEvent> Events);