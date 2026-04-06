var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddInMemoryCollection(LoadDotEnvFromWorkspaceRoot());

builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
	["CosmosDb:Endpoint"] = FirstValue(Environment.GetEnvironmentVariable("CosmosDb__Endpoint"), builder.Configuration["COSMOS_DB_ENDPOINT"], builder.Configuration["CosmosDb:Endpoint"]),
	["CosmosDb:Key"] = FirstValue(Environment.GetEnvironmentVariable("CosmosDb__Key"), builder.Configuration["COSMOS_DB_KEY"], builder.Configuration["CosmosDb:Key"]),
	["CosmosDb:DatabaseName"] = FirstValue(Environment.GetEnvironmentVariable("CosmosDb__DatabaseName"), builder.Configuration["COSMOS_DB_DATABASE_NAME"], builder.Configuration["CosmosDb:DatabaseName"])
});

builder.Services
	.AddOptions<ChangeFeedProcessor.Configuration.CosmosDbOptions>()
	.Bind(builder.Configuration.GetSection(ChangeFeedProcessor.Configuration.CosmosDbOptions.SectionName))
	.ValidateOnStart();

builder.Services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<ChangeFeedProcessor.Configuration.CosmosDbOptions>, ChangeFeedProcessor.Configuration.CosmosDbOptionsValidator>();
builder.Services.AddSingleton(new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
builder.Services.AddSingleton(ChangeFeedProcessor.Infrastructure.CosmosClientFactory.Create);
builder.Services.AddSingleton<ChangeFeedProcessor.Services.IChangeFeedFailureStore, ChangeFeedProcessor.Services.CosmosChangeFeedFailureStore>();
builder.Services.AddSingleton<ChangeFeedProcessor.Services.IOrderSummaryProjectionRebuilder, ChangeFeedProcessor.Services.CosmosOrderSummaryProjectionRebuilder>();
builder.Services.AddSingleton<ChangeFeedProcessor.Services.IOrderEventProjectionHandler, ChangeFeedProcessor.Services.OrderSummaryProjector>();
builder.Services.AddSingleton<ChangeFeedProcessor.Services.IOrderEventProjectionHandler, ChangeFeedProcessor.Services.RecommendationProjector>();
builder.Services.AddSingleton<ChangeFeedProcessor.Services.IOrderEventProjectionDispatcher, ChangeFeedProcessor.Services.OrderEventProjectionDispatcher>();
builder.Services.AddHostedService<ChangeFeedProcessor.Services.OrderEventsProjectionWorker>();

var host = builder.Build();
await host.RunAsync();

static Dictionary<string, string?> LoadDotEnvFromWorkspaceRoot()
{
	DirectoryInfo? directory = new(Directory.GetCurrentDirectory());

	while (directory is not null)
	{
		string envPath = Path.Combine(directory.FullName, ".env");
		if (File.Exists(envPath))
		{
			var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
			foreach (string rawLine in File.ReadAllLines(envPath))
			{
				string line = rawLine.Trim();
				if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
				{
					continue;
				}

				int separator = line.IndexOf('=');
				if (separator <= 0)
				{
					continue;
				}

				string key = line[..separator].Trim();
				string value = line[(separator + 1)..].Trim();
				if (value.Length >= 2 && ((value.StartsWith('"') && value.EndsWith('"')) || (value.StartsWith('\'') && value.EndsWith('\''))))
				{
					value = value[1..^1];
				}

				values[key] = value;
			}

			return values;
		}

		directory = directory.Parent;
	}

	return [];
}

static string FirstValue(params string?[] candidates)
{
	foreach (string? candidate in candidates)
	{
		if (!string.IsNullOrWhiteSpace(candidate))
		{
			return candidate;
		}
	}

	return string.Empty;
}
