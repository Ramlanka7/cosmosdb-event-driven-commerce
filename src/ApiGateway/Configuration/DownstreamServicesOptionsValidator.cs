using Microsoft.Extensions.Options;

namespace ApiGateway.Configuration;

internal sealed class DownstreamServicesOptionsValidator : IValidateOptions<DownstreamServicesOptions>
{
    public ValidateOptionsResult Validate(string? name, DownstreamServicesOptions options)
    {
        List<string> failures = [];

        ValidateUri(options.OrderServiceBaseUrl, "DownstreamServices:OrderServiceBaseUrl", failures);
        ValidateUri(options.ReadModelServiceBaseUrl, "DownstreamServices:ReadModelServiceBaseUrl", failures);
        ValidateUri(options.NotificationServiceBaseUrl, "DownstreamServices:NotificationServiceBaseUrl", failures);
        ValidateUri(options.RecommendationServiceBaseUrl, "DownstreamServices:RecommendationServiceBaseUrl", failures);

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }

    private static void ValidateUri(string value, string settingName, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add($"{settingName} is required.");
            return;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? endpointUri) || !endpointUri.Scheme.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"{settingName} must be a valid absolute HTTP or HTTPS URI.");
        }
    }
}