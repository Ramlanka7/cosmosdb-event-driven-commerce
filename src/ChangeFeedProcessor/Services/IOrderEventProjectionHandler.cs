using Commerce.Eventing.Contracts;

namespace ChangeFeedProcessor.Services;

internal interface IOrderEventProjectionHandler
{
    string Name { get; }

    Task ProjectAsync(OrderEventEnvelope envelope, CancellationToken cancellationToken);
}