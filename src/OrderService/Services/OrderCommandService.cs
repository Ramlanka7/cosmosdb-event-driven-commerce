using OrderService.Contracts;
using OrderService.Domain;
using OrderService.Infrastructure;

namespace OrderService.Services;

internal interface IOrderCommandService
{
    Task<OrderCommandResponse> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken);

    Task<OrderCommandResponse> ConfirmOrderAsync(string orderId, ConfirmOrderRequest? request, CancellationToken cancellationToken);

    Task<OrderStream> GetStreamAsync(string orderId, CancellationToken cancellationToken);
}

internal sealed class OrderCommandService(IOrderEventStore orderEventStore) : IOrderCommandService
{
    public async Task<OrderCommandResponse> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        string orderId = Guid.NewGuid().ToString("N");
        OrderAggregate aggregate = OrderAggregate.CreateNew(orderId);
        OrderCreated created = aggregate.Create(request.UserId, request.Items, DateTime.UtcNow);

        StoredOrderEvent storedEvent = await orderEventStore.AppendAsync(
            orderId,
            expectedVersion: 0,
            created,
            BuildMetadata(request.CorrelationId, request.CorrelationId),
            cancellationToken);

        return new OrderCommandResponse(orderId, OrderStatus.Pending.ToString(), storedEvent.SequenceNumber, storedEvent.OccurredAtUtc);
    }

    public async Task<OrderCommandResponse> ConfirmOrderAsync(string orderId, ConfirmOrderRequest? request, CancellationToken cancellationToken)
    {
        OrderStream stream = await orderEventStore.ReadStreamAsync(orderId, cancellationToken);

        if (stream.Version == 0)
        {
            throw new KeyNotFoundException($"Order '{orderId}' does not exist.");
        }

        OrderAggregate aggregate = OrderAggregate.FromHistory(orderId, stream.Events.Select(orderEvent => orderEvent.Payload));
        OrderConfirmed confirmed = aggregate.Confirm(DateTime.UtcNow);

        StoredOrderEvent storedEvent = await orderEventStore.AppendAsync(
            orderId,
            stream.Version,
            confirmed,
            BuildMetadata(request?.CorrelationId, request?.CausationId),
            cancellationToken);

        return new OrderCommandResponse(orderId, OrderStatus.Confirmed.ToString(), storedEvent.SequenceNumber, storedEvent.OccurredAtUtc);
    }

    public Task<OrderStream> GetStreamAsync(string orderId, CancellationToken cancellationToken)
        => orderEventStore.ReadStreamAsync(orderId, cancellationToken);

    private static EventMetadata BuildMetadata(string? correlationId, string? causationId)
    {
        string commandId = Guid.NewGuid().ToString("N");
        return new EventMetadata(correlationId ?? commandId, causationId ?? commandId);
    }
}