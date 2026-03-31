using Commerce.Eventing.Contracts;

namespace ChangeFeedProcessor.Services;

internal interface IOrderSummaryProjector
{
    Task ProjectAsync(OrderEventEnvelope envelope, CancellationToken cancellationToken);
}