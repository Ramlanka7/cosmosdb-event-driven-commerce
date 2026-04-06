using System.Text.Json;
using OrderService.Domain;
using OrderService.Infrastructure;
using Xunit;

namespace OrderService.Tests;

public sealed class OrderEventDocumentMapperTests
{
    [Fact]
    public void Mapper_round_trips_order_created_event()
    {
        JsonSerializerOptions serializerOptions = new(JsonSerializerDefaults.Web);
        OrderEventDocumentMapper mapper = new(serializerOptions);
        OrderCreated orderCreated = new(
            "order-42",
            "user-42",
            [new OrderItem("sku-42", 1, 99.95m)],
            new DateTime(2026, 3, 30, 13, 0, 0, DateTimeKind.Utc));

        CosmosEventDocument document = mapper.ToDocument("order-42", 1, orderCreated, new EventMetadata("corr-1", "cause-1"));
        StoredOrderEvent stored = mapper.ToStoredEvent(document);
        OrderCreated payload = Assert.IsType<OrderCreated>(stored.Payload);

        Assert.Equal("order-42:0000000001", document.id);
        Assert.Equal("order-42", stored.AggregateId);
        Assert.Equal("corr-1", stored.CorrelationId);
        Assert.Equal("cause-1", stored.CausationId);
        Assert.Equal("user-42", payload.UserId);
        Assert.Single(payload.Items);
        Assert.Equal("sku-42", payload.Items.Single().Sku);
    }
}