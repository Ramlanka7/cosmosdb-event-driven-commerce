using Commerce.Eventing.ReadModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using ReadModelService.Configuration;
using ReadModelService.Contracts;
using ReadModelService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddInMemoryCollection(LoadDotEnvFromWorkspaceRoot());

builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
	["CosmosDb:Endpoint"] = FirstValue(Environment.GetEnvironmentVariable("CosmosDb__Endpoint"), builder.Configuration["COSMOS_DB_ENDPOINT"], builder.Configuration["CosmosDb:Endpoint"]),
	["CosmosDb:Key"] = FirstValue(Environment.GetEnvironmentVariable("CosmosDb__Key"), builder.Configuration["COSMOS_DB_KEY"], builder.Configuration["CosmosDb:Key"]),
	["CosmosDb:DatabaseName"] = FirstValue(Environment.GetEnvironmentVariable("CosmosDb__DatabaseName"), builder.Configuration["COSMOS_DB_DATABASE_NAME"], builder.Configuration["CosmosDb:DatabaseName"])
});

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services
	.AddOptions<CosmosDbOptions>()
	.Bind(builder.Configuration.GetSection(CosmosDbOptions.SectionName))
	.ValidateOnStart();

builder.Services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<CosmosDbOptions>, CosmosDbOptionsValidator>();
builder.Services.AddSingleton(CosmosClientFactory.Create);
builder.Services.AddSingleton<IOrderSummaryQueryService, CosmosOrderSummaryQueryService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().WithMethods("GET", "POST").AllowAnyHeader());
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();

if (!app.Environment.IsDevelopment())
{
	app.UseHsts();
	app.UseHttpsRedirection();
}

app.MapHealthChecks("/health");

RouteGroupBuilder users = app.MapGroup("/users/{userId}");

users.MapGet("/orders", async Task<IResult> (
	HttpContext httpContext,
	string userId,
	[AsParameters] ListOrderSummariesRequest request,
	IOrderSummaryQueryService queryService,
	CancellationToken cancellationToken) =>
{
	Dictionary<string, string[]>? validationFailures = ValidateRequest(request);

	if (validationFailures is not null)
	{
		return TypedResults.ValidationProblem(validationFailures);
	}

	OrderSummaryQuery query = request.ToQuery(userId);
	OrderSummaryPage page = await queryService.ListOrdersAsync(query, cancellationToken);

	if (!string.IsNullOrWhiteSpace(page.ContinuationToken))
	{
		httpContext.Response.Headers.Append("x-continuation-token", page.ContinuationToken);
	}

	httpContext.Response.Headers.Append("x-page-size", page.PageSize.ToString(System.Globalization.CultureInfo.InvariantCulture));

	IReadOnlyCollection<OrderSummaryResponse> response = page.Items.Select(MapResponse).ToArray();
	return TypedResults.Ok(response);
});

users.MapGet("/orders/{orderId}", async Task<IResult> (
	string userId,
	string orderId,
	IOrderSummaryQueryService queryService,
	CancellationToken cancellationToken) =>
{
	OrderSummaryDocument? document = await queryService.GetOrderAsync(userId, orderId, cancellationToken);

	return document is null
		? TypedResults.NotFound(new ProblemDetails
		{
			Title = "Order summary not found.",
			Detail = $"No projected order summary exists for order '{orderId}' and user '{userId}'.",
			Status = StatusCodes.Status404NotFound
		})
		: TypedResults.Ok(MapResponse(document));
});

app.Run();

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

static OrderSummaryResponse MapResponse(OrderSummaryDocument document) => new(
	document.orderId,
	document.userId,
	document.status,
	document.totalAmount,
	document.createdAtUtc,
	document.lastUpdatedAtUtc,
	document.items.Select(item => new OrderItemResponse(item.Sku, item.Quantity, item.UnitPrice)).ToArray());

static Dictionary<string, string[]>? ValidateRequest(ListOrderSummariesRequest request)
{
	Dictionary<string, string[]> failures = [];

	if (request.PageSize is <= 0)
	{
		failures[nameof(request.PageSize)] = ["PageSize must be greater than zero."];
	}
	else if (request.PageSize is > ListOrderSummariesRequest.MaxPageSize)
	{
		failures[nameof(request.PageSize)] = [$"PageSize cannot exceed {ListOrderSummariesRequest.MaxPageSize}."];
	}

	if (!ListOrderSummariesRequest.TryNormalizeStatus(request.Status, out _))
	{
		failures[nameof(request.Status)] = [$"Status must be one of: {string.Join(", ", ListOrderSummariesRequest.SupportedStatuses)}."];
	}

	if (request.UpdatedAfterUtc is not null && request.UpdatedBeforeUtc is not null && request.UpdatedAfterUtc > request.UpdatedBeforeUtc)
	{
		failures[nameof(request.UpdatedAfterUtc)] = ["UpdatedAfterUtc must be earlier than or equal to UpdatedBeforeUtc."];
	}

	return failures.Count == 0 ? null : failures;
}
