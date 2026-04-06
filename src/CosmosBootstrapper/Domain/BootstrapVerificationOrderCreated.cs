namespace CosmosBootstrapper.Domain;

internal sealed class BootstrapVerificationOrderCreated
{
    public string orderId { get; init; } = string.Empty;

    public string userId { get; init; } = string.Empty;

    public IReadOnlyCollection<BootstrapVerificationOrderItem> items { get; init; } = [];

    public DateTime occurredAtUtc { get; init; }
}

internal sealed class BootstrapVerificationOrderItem
{
    public string sku { get; init; } = string.Empty;

    public int quantity { get; init; }

    public decimal unitPrice { get; init; }
}