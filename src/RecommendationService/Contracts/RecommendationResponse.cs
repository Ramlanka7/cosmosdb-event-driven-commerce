namespace RecommendationService.Contracts;

internal sealed record RecommendationResponse(
    string UserId,
    DateTime? LastRefreshedAtUtc,
    IReadOnlyCollection<RecommendationItemResponse> Suggestions);

internal sealed record RecommendationItemResponse(string Sku, decimal Score, string Reason);