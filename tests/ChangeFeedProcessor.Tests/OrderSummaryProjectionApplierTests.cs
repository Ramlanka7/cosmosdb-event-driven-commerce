using ChangeFeedProcessor.Services;
using Commerce.Eventing.Contracts;
using Commerce.Eventing.ReadModels;
using Xunit;

namespace ChangeFeedProcessor.Tests;

public sealed class OrderSummaryProjectionApplierTests
{
    [Fact]
    public void Apply_order_created_builds_pending_summary()
    {
        OrderEventEnvelope envelope = new(
            "order-100",
            1,
            OrderEventTypes.OrderCreated,
            1,
            new DateTime(2026, 3, 30, 10, 0, 0, DateTimeKind.Utc),
            "corr-1",
            "cause-1",
            new OrderCreatedIntegrationEvent(
                "order-100",
                "user-100",
                [new OrderItemData("sku-1", 2, 12.5m), new OrderItemData("sku-2", 1, 9m)],
                new DateTime(2026, 3, 30, 10, 0, 0, DateTimeKind.Utc)));

        OrderSummaryDocument document = OrderSummaryProjectionApplier.Apply(null, envelope);

        Assert.Equal("order-100", document.id);
        Assert.Equal("Pending", document.status);
        Assert.Equal(34m, document.totalAmount);
        Assert.Equal(1, document.lastProcessedSequenceNumber);
        Assert.Equal("user-100", document.userId);
    }

    [Fact]
    public void Apply_order_confirmed_updates_existing_summary()
    {
        OrderSummaryDocument current = new()
        {
            id = "order-100",
            documentType = "order-summary",
            orderId = "order-100",
            userId = "user-100",
            status = "Pending",
            items = [new OrderItemData("sku-1", 1, 50m)],
            totalAmount = 50m,
            createdAtUtc = new DateTime(2026, 3, 30, 10, 0, 0, DateTimeKind.Utc),
            lastUpdatedAtUtc = new DateTime(2026, 3, 30, 10, 0, 0, DateTimeKind.Utc),
            lastProcessedSequenceNumber = 1,
            projectionVersion = 1
        };

        OrderEventEnvelope envelope = new(
            "order-100",
            2,
            OrderEventTypes.OrderConfirmed,
            1,
            new DateTime(2026, 3, 30, 10, 5, 0, DateTimeKind.Utc),
            "corr-2",
            "cause-2",
            new OrderConfirmedIntegrationEvent("order-100", "user-100", new DateTime(2026, 3, 30, 10, 5, 0, DateTimeKind.Utc)));

        OrderSummaryDocument updated = OrderSummaryProjectionApplier.Apply(current, envelope);

        Assert.Equal("Confirmed", updated.status);
        Assert.Equal(2, updated.lastProcessedSequenceNumber);
        Assert.Equal(new DateTime(2026, 3, 30, 10, 5, 0, DateTimeKind.Utc), updated.lastUpdatedAtUtc);
        Assert.Single(updated.items);
    }

    [Fact]
    public void Apply_duplicate_sequence_keeps_existing_projection()
    {
        OrderSummaryDocument current = new()
        {
            id = "order-100",
            documentType = "order-summary",
            orderId = "order-100",
            userId = "user-100",
            status = "Pending",
            items = [new OrderItemData("sku-1", 1, 50m)],
            totalAmount = 50m,
            createdAtUtc = new DateTime(2026, 3, 30, 10, 0, 0, DateTimeKind.Utc),
            lastUpdatedAtUtc = new DateTime(2026, 3, 30, 10, 0, 0, DateTimeKind.Utc),
            lastProcessedSequenceNumber = 2,
            projectionVersion = 1
        };

        OrderEventEnvelope envelope = new(
            "order-100",
            1,
            OrderEventTypes.OrderCreated,
            1,
            new DateTime(2026, 3, 30, 9, 55, 0, DateTimeKind.Utc),
            "corr-3",
            "cause-3",
            new OrderCreatedIntegrationEvent(
                "order-100",
                "user-100",
                [new OrderItemData("sku-1", 1, 50m)],
                new DateTime(2026, 3, 30, 9, 55, 0, DateTimeKind.Utc)));

        OrderSummaryDocument updated = OrderSummaryProjectionApplier.Apply(current, envelope);

        Assert.Same(current, updated);
    }
}