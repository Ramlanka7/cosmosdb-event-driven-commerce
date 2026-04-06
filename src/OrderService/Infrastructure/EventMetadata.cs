namespace OrderService.Infrastructure;

internal sealed record EventMetadata(string? CorrelationId, string? CausationId);