namespace OrderService.Contracts;

internal sealed record OrderCommandResponse(string OrderId, string Status, int Version, DateTime OccurredAtUtc);