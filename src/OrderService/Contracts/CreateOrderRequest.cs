namespace OrderService.Contracts;

internal sealed record CreateOrderRequest(string UserId, IReadOnlyCollection<CreateOrderItemRequest> Items, string? CorrelationId);

internal sealed record CreateOrderItemRequest(string Sku, int Quantity, decimal UnitPrice);