namespace Commerce.Eventing.Contracts;

public sealed record OrderItemData(string Sku, int Quantity, decimal UnitPrice);