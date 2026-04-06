using Microsoft.Extensions.Options;

namespace CosmosBootstrapper.Configuration;

internal sealed class CosmosDbOptionsValidator : IValidateOptions<CosmosDbOptions>
{
    public ValidateOptionsResult Validate(string? name, CosmosDbOptions options)
    {
        List<string> failures = [];

        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            failures.Add("CosmosDb:Endpoint is required.");
        }
        else if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out Uri? endpointUri) || !endpointUri.Scheme.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("CosmosDb:Endpoint must be a valid absolute HTTP or HTTPS URI.");
        }

        if (string.IsNullOrWhiteSpace(options.Key))
        {
            failures.Add("CosmosDb:Key is required.");
        }

        if (string.IsNullOrWhiteSpace(options.DatabaseName))
        {
            failures.Add("CosmosDb:DatabaseName is required.");
        }

        if (options.DatabaseManualThroughput is <= 0)
        {
            failures.Add("CosmosDb:DatabaseManualThroughput must be greater than 0 when specified.");
        }

        if (options.PreferredRegions.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add("CosmosDb:PreferredRegions cannot contain empty values.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}