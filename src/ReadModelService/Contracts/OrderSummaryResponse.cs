namespace ReadModelService.Contracts;

internal sealed record OrderSummaryResponse(
    string OrderId,
    string UserId,
    string Status,
    decimal TotalAmount,
    DateTime CreatedAtUtc,
    DateTime LastUpdatedAtUtc,
    IReadOnlyCollection<OrderItemResponse> Items);

internal sealed record OrderItemResponse(string Sku, int Quantity, decimal UnitPrice);