namespace OrderService.Contracts;

internal sealed record ConfirmOrderRequest(string? CorrelationId, string? CausationId);