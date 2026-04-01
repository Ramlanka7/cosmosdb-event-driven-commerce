using Commerce.Eventing.Contracts;
using Commerce.Eventing.Infrastructure;

namespace ChangeFeedProcessor.Services;

internal interface IOrderEventProjectionDispatcher
{
    Task DispatchAsync(CosmosEventDocument document, OrderEventEnvelope envelope, CancellationToken cancellationToken);
}

internal sealed class OrderEventProjectionDispatcher(
    IEnumerable<IOrderEventProjectionHandler> handlers,
    IChangeFeedFailureStore failureStore,
    ILogger<OrderEventProjectionDispatcher> logger) : IOrderEventProjectionDispatcher
{
    private readonly IReadOnlyList<IOrderEventProjectionHandler> _handlers = handlers.ToArray();

    public async Task DispatchAsync(CosmosEventDocument document, OrderEventEnvelope envelope, CancellationToken cancellationToken)
    {
        foreach (IOrderEventProjectionHandler handler in _handlers)
        {
            using IDisposable? scope = logger.BeginScope(new Dictionary<string, object?>
            {
                ["AggregateId"] = envelope.AggregateId,
                ["EventType"] = envelope.EventType,
                ["SequenceNumber"] = envelope.SequenceNumber,
                ["HandlerName"] = handler.Name
            });

            try
            {
                await handler.ProjectAsync(envelope, cancellationToken);
            }
            catch (Exception exception)
            {
                await failureStore.RecordAsync(document, envelope, handler.Name, exception, cancellationToken);

                logger.LogError(
                    exception,
                    "Projection handler {HandlerName} failed for event {EventType} on aggregate {AggregateId} at sequence {SequenceNumber}.",
                    handler.Name,
                    envelope.EventType,
                    envelope.AggregateId,
                    envelope.SequenceNumber);

                throw;
            }
        }
    }
}