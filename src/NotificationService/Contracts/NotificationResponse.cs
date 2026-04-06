namespace NotificationService.Contracts;

internal sealed record NotificationResponse(string OrderId, string NotificationType, string Message, DateTime OccurredAtUtc);