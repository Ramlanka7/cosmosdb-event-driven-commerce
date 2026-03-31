using Commerce.Eventing.Contracts;
using Commerce.Eventing.ReadModels;
using RecommendationService.Services;
using Xunit;

namespace ChangeFeedProcessor.Tests;

public sealed class RecommendationProjectionApplierTests
{
    [Fact]
    public void Apply_order_created_accumulates_recommendation_scores()
    {
        RecommendationDocument current = new()
        {
            id = "user-42",
            documentType = "recommendation-profile",
            userId = "user-42",
            suggestedSkus = [new RecommendedSku("sku-legacy", 10m, "Purchased in prior orders")],
            lastRefreshedAtUtc = new DateTime(2026, 3, 29, 9, 0, 0, DateTimeKind.Utc),
            lastProcessedSequenceNumber = 1,
            projectionVersion = 1
        };

        OrderEventEnvelope envelope = new(
            "order-42",
            2,
            OrderEventTypes.OrderCreated,
            1,
            new DateTime(2026, 3, 30, 11, 0, 0, DateTimeKind.Utc),
            "corr-1",
            "cause-1",
            new OrderCreatedIntegrationEvent(
                "order-42",
                "user-42",
                [new OrderItemData("sku-new", 2, 15m), new OrderItemData("sku-legacy", 1, 5m)],
                new DateTime(2026, 3, 30, 11, 0, 0, DateTimeKind.Utc)));

        RecommendationDocument updated = RecommendationProjectionApplier.Apply(current, envelope);

        Assert.Equal("user-42", updated.id);
        Assert.Equal(2, updated.lastProcessedSequenceNumber);
        Assert.Equal("sku-new", updated.suggestedSkus.First().Sku);
        Assert.Equal(30m, updated.suggestedSkus.First().Score);
        Assert.Equal(15m, updated.suggestedSkus.Single(item => item.Sku == "sku-legacy").Score);
    }

    [Fact]
    public void Apply_order_confirmed_advances_projection_without_changing_scores()
    {
        RecommendationDocument current = new()
        {
            id = "user-42",
            documentType = "recommendation-profile",
            userId = "user-42",
            suggestedSkus = [new RecommendedSku("sku-1", 12m, "Purchased in prior orders")],
            lastRefreshedAtUtc = new DateTime(2026, 3, 30, 11, 0, 0, DateTimeKind.Utc),
            lastProcessedSequenceNumber = 1,
            projectionVersion = 1
        };

        OrderEventEnvelope envelope = new(
            "order-42",
            2,
            OrderEventTypes.OrderConfirmed,
            1,
            new DateTime(2026, 3, 30, 12, 0, 0, DateTimeKind.Utc),
            "corr-2",
            "cause-2",
            new OrderConfirmedIntegrationEvent("order-42", "user-42", new DateTime(2026, 3, 30, 12, 0, 0, DateTimeKind.Utc)));

        RecommendationDocument updated = RecommendationProjectionApplier.Apply(current, envelope);

        Assert.Single(updated.suggestedSkus);
        Assert.Equal("sku-1", updated.suggestedSkus.Single().Sku);
        Assert.Equal(2, updated.lastProcessedSequenceNumber);
        Assert.Equal(new DateTime(2026, 3, 30, 12, 0, 0, DateTimeKind.Utc), updated.lastRefreshedAtUtc);
    }
}