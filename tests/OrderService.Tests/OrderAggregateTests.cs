using OrderService.Contracts;
using OrderService.Domain;
using Xunit;

namespace OrderService.Tests;

public sealed class OrderAggregateTests
{
    [Fact]
    public void Create_requires_at_least_one_item()
    {
        OrderAggregate aggregate = OrderAggregate.CreateNew("order-1");

        OrderDomainException exception = Assert.Throws<OrderDomainException>(() =>
            aggregate.Create("user-1", Array.Empty<CreateOrderItemRequest>(), new DateTime(2026, 3, 30, 12, 0, 0, DateTimeKind.Utc)));

        Assert.Equal("At least one order item is required.", exception.Message);
    }

    [Fact]
    public void Confirm_after_create_emits_confirmation_event()
    {
        OrderCreated created = new(
            "order-1",
            "user-1",
            [new OrderItem("sku-1", 2, 14.50m)],
            new DateTime(2026, 3, 30, 12, 0, 0, DateTimeKind.Utc));

        OrderAggregate aggregate = OrderAggregate.FromHistory("order-1", [created]);

        OrderConfirmed confirmed = aggregate.Confirm(new DateTime(2026, 3, 30, 12, 5, 0, DateTimeKind.Utc));
        OrderAggregate rehydrated = OrderAggregate.FromHistory("order-1", [created, confirmed]);

        Assert.Equal("order-1", confirmed.OrderId);
        Assert.Equal("user-1", confirmed.UserId);
        Assert.Equal(OrderStatus.Confirmed, rehydrated.Status);
        Assert.Equal(2, rehydrated.Version);
    }
}