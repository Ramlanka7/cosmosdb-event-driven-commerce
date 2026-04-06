namespace OrderService.Domain;

internal sealed record OrderItem(string Sku, int Quantity, decimal UnitPrice);