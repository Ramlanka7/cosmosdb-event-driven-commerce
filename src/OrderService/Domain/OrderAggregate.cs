using OrderService.Contracts;

namespace OrderService.Domain;

internal sealed class OrderAggregate
{
    private readonly List<OrderItem> _items = [];

    private OrderAggregate(string id)
    {
        Id = id;
    }

    public string Id { get; }

    public string UserId { get; private set; } = string.Empty;

    public OrderStatus Status { get; private set; } = OrderStatus.Pending;

    public int Version { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items;

    public static OrderAggregate CreateNew(string orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new OrderDomainException("Order id is required.");
        }

        return new OrderAggregate(orderId);
    }

    public static OrderAggregate FromHistory(string orderId, IEnumerable<IOrderEvent> events)
    {
        OrderAggregate aggregate = CreateNew(orderId);

        foreach (IOrderEvent orderEvent in events)
        {
            aggregate.Apply(orderEvent);
            aggregate.Version++;
        }

        return aggregate;
    }

    public OrderCreated Create(string userId, IReadOnlyCollection<CreateOrderItemRequest> items, DateTime occurredAtUtc)
    {
        if (Version > 0)
        {
            throw new OrderDomainException($"Order '{Id}' already exists.");
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new OrderDomainException("User id is required.");
        }

        if (items.Count == 0)
        {
            throw new OrderDomainException("At least one order item is required.");
        }

        OrderItem[] normalizedItems = items
            .Select(item => new OrderItem(
                NormalizeSku(item.Sku),
                item.Quantity > 0 ? item.Quantity : throw new OrderDomainException("Quantity must be greater than zero."),
                item.UnitPrice > 0 ? item.UnitPrice : throw new OrderDomainException("Unit price must be greater than zero.")))
            .ToArray();

        if (normalizedItems.Any(item => string.IsNullOrWhiteSpace(item.Sku)))
        {
            throw new OrderDomainException("Each order item must include a sku.");
        }

        return new OrderCreated(Id, userId.Trim(), normalizedItems, occurredAtUtc);
    }

    public OrderConfirmed Confirm(DateTime occurredAtUtc)
    {
        if (Version == 0)
        {
            throw new OrderDomainException($"Order '{Id}' does not exist.");
        }

        if (Status == OrderStatus.Confirmed)
        {
            throw new OrderDomainException($"Order '{Id}' is already confirmed.");
        }

        return new OrderConfirmed(Id, UserId, occurredAtUtc);
    }

    public void Apply(IOrderEvent orderEvent)
    {
        switch (orderEvent)
        {
            case OrderCreated created:
                UserId = created.UserId;
                Status = OrderStatus.Pending;
                _items.Clear();
                _items.AddRange(created.Items);
                break;
            case OrderConfirmed:
                Status = OrderStatus.Confirmed;
                break;
            default:
                throw new NotSupportedException($"Unsupported order event '{orderEvent.EventType}' version {orderEvent.EventVersion}.");
        }
    }

    private static string NormalizeSku(string sku) => string.IsNullOrWhiteSpace(sku) ? string.Empty : sku.Trim();
}