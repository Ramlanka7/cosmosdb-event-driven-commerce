namespace ReadModelService.Infrastructure;

internal sealed record OrderSummaryQuery(
    string UserId,
    string? Status,
    DateTime? UpdatedAfterUtc,
    DateTime? UpdatedBeforeUtc,
    int PageSize,
    string? ContinuationToken);

internal sealed record OrderSummaryPage(
    IReadOnlyCollection<Commerce.Eventing.ReadModels.OrderSummaryDocument> Items,
    string? ContinuationToken,
    int PageSize,
    double RequestCharge);