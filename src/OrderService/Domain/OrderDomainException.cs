namespace OrderService.Domain;

internal sealed class OrderDomainException(string message) : Exception(message);