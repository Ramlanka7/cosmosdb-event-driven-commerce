namespace ApiGateway.Configuration;

internal sealed class DownstreamServicesOptions
{
    public const string SectionName = "DownstreamServices";

    public string OrderServiceBaseUrl { get; init; } = string.Empty;

    public string ReadModelServiceBaseUrl { get; init; } = string.Empty;

    public string NotificationServiceBaseUrl { get; init; } = string.Empty;

    public string RecommendationServiceBaseUrl { get; init; } = string.Empty;
}