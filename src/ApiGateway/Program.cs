using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using ApiGateway.Configuration;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Http.Resilience;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services
	.AddOptions<DownstreamServicesOptions>()
	.Bind(builder.Configuration.GetSection(DownstreamServicesOptions.SectionName))
	.ValidateOnStart();

builder.Services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<DownstreamServicesOptions>, DownstreamServicesOptionsValidator>();

// --- CORS ---
builder.Services.AddCors(options =>
{
	string[] origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

	options.AddDefaultPolicy(policy =>
	{
		if (origins.Length > 0)
		{
			policy.WithOrigins(origins);
		}

		policy
			.WithMethods("GET", "POST")
			.WithHeaders("Content-Type", "X-Api-Key", "X-Continuation-Token")
			.WithExposedHeaders("X-Continuation-Token", "X-Page-Size");
	});
});

// --- Rate Limiting ---
builder.Services.AddRateLimiter(options =>
{
	options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
	options.AddFixedWindowLimiter("fixed", limiterOptions =>
	{
		limiterOptions.PermitLimit = 100;
		limiterOptions.Window = TimeSpan.FromMinutes(1);
		limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
		limiterOptions.QueueLimit = 10;
	});
});

// --- HTTP Clients with Resilience ---
void ConfigureClient(IHttpClientBuilder clientBuilder) =>
	clientBuilder.AddStandardResilienceHandler();

ConfigureClient(builder.Services.AddHttpClient("order-service", (serviceProvider, client) =>
{
	DownstreamServicesOptions options = serviceProvider
		.GetRequiredService<Microsoft.Extensions.Options.IOptions<DownstreamServicesOptions>>()
		.Value;
	client.BaseAddress = new Uri(options.OrderServiceBaseUrl);
}));
ConfigureClient(builder.Services.AddHttpClient("read-model-service", (serviceProvider, client) =>
{
	DownstreamServicesOptions options = serviceProvider
		.GetRequiredService<Microsoft.Extensions.Options.IOptions<DownstreamServicesOptions>>()
		.Value;
	client.BaseAddress = new Uri(options.ReadModelServiceBaseUrl);
}));
ConfigureClient(builder.Services.AddHttpClient("notification-service", (serviceProvider, client) =>
{
	DownstreamServicesOptions options = serviceProvider
		.GetRequiredService<Microsoft.Extensions.Options.IOptions<DownstreamServicesOptions>>()
		.Value;
	client.BaseAddress = new Uri(options.NotificationServiceBaseUrl);
}));
ConfigureClient(builder.Services.AddHttpClient("recommendation-service", (serviceProvider, client) =>
{
	DownstreamServicesOptions options = serviceProvider
		.GetRequiredService<Microsoft.Extensions.Options.IOptions<DownstreamServicesOptions>>()
		.Value;
	client.BaseAddress = new Uri(options.RecommendationServiceBaseUrl);
}));

var app = builder.Build();

app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
	app.UseHsts();
	app.UseHttpsRedirection();
}

app.UseCors();
app.UseRateLimiter();

// --- API Key Authentication Middleware ---
string? configuredApiKey = app.Configuration["Authentication:ApiKey"];

app.Use(async (context, next) =>
{
	if (context.Request.Path.StartsWithSegments("/health"))
	{
		await next();
		return;
	}

	if (string.IsNullOrWhiteSpace(configuredApiKey))
	{
		await next();
		return;
	}

	if (!context.Request.Headers.TryGetValue("X-Api-Key", out Microsoft.Extensions.Primitives.StringValues providedKey) ||
		string.IsNullOrWhiteSpace(providedKey))
	{
		context.Response.StatusCode = StatusCodes.Status401Unauthorized;
		await context.Response.WriteAsJsonAsync(new { title = "Missing API key.", status = 401 });
		return;
	}

	bool keyValid = CryptographicOperations.FixedTimeEquals(
		System.Text.Encoding.UTF8.GetBytes(configuredApiKey),
		System.Text.Encoding.UTF8.GetBytes(providedKey.ToString()));

	if (!keyValid)
	{
		context.Response.StatusCode = StatusCodes.Status403Forbidden;
		await context.Response.WriteAsJsonAsync(new { title = "Invalid API key.", status = 403 });
		return;
	}

	await next();
});

app.MapHealthChecks("/health");

app.MapPost("/orders", (HttpContext context, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
	=> ProxyAsync(context, httpClientFactory.CreateClient("order-service"), "/orders", cancellationToken))
	.RequireRateLimiting("fixed");

app.MapPost("/orders/{orderId}/confirm", (HttpContext context, string orderId, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
	=> ProxyAsync(context, httpClientFactory.CreateClient("order-service"), $"/orders/{orderId}/confirm", cancellationToken))
	.RequireRateLimiting("fixed");

app.MapGet("/users/{userId}/orders", (HttpContext context, string userId, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
	=> ProxyAsync(context, httpClientFactory.CreateClient("read-model-service"), $"/users/{userId}/orders", cancellationToken))
	.RequireRateLimiting("fixed");

app.MapGet("/users/{userId}/orders/{orderId}", (HttpContext context, string userId, string orderId, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
	=> ProxyAsync(context, httpClientFactory.CreateClient("read-model-service"), $"/users/{userId}/orders/{orderId}", cancellationToken))
	.RequireRateLimiting("fixed");

app.MapGet("/users/{userId}/notifications", (HttpContext context, string userId, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
	=> ProxyAsync(context, httpClientFactory.CreateClient("notification-service"), $"/users/{userId}/notifications", cancellationToken))
	.RequireRateLimiting("fixed");

app.MapGet("/users/{userId}/recommendations", (HttpContext context, string userId, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
	=> ProxyAsync(context, httpClientFactory.CreateClient("recommendation-service"), $"/users/{userId}/recommendations", cancellationToken))
	.RequireRateLimiting("fixed");

app.Run();

static async Task ProxyAsync(HttpContext context, HttpClient client, string relativePath, CancellationToken cancellationToken)
{
	string pathWithQuery = context.Request.QueryString.HasValue
		? $"{relativePath}{context.Request.QueryString.Value}"
		: relativePath;

	using HttpRequestMessage request = new(new HttpMethod(context.Request.Method), pathWithQuery);

	if (context.Request.ContentLength is > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
	{
		using MemoryStream bodyBuffer = new();
		await context.Request.Body.CopyToAsync(bodyBuffer, cancellationToken);
		request.Content = new ByteArrayContent(bodyBuffer.ToArray());

		if (!string.IsNullOrWhiteSpace(context.Request.ContentType))
		{
			request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(context.Request.ContentType);
		}
	}

	using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

	context.Response.StatusCode = (int)response.StatusCode;

	if (response.Content.Headers.ContentType is not null)
	{
		context.Response.ContentType = response.Content.Headers.ContentType.ToString();
	}

	if (response.Headers.Location is not null)
	{
		context.Response.Headers.Location = response.Headers.Location.ToString();
	}

	foreach (System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.IEnumerable<string>> header in response.Headers)
	{
		if (string.Equals(header.Key, "x-continuation-token", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(header.Key, "x-page-size", StringComparison.OrdinalIgnoreCase))
		{
			context.Response.Headers[header.Key] = header.Value.ToArray();
		}
	}

	await response.Content.CopyToAsync(context.Response.Body, cancellationToken);
}
