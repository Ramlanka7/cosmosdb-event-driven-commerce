using System.Net.Http.Headers;
using ApiGateway.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services
	.AddOptions<DownstreamServicesOptions>()
	.Bind(builder.Configuration.GetSection(DownstreamServicesOptions.SectionName))
	.ValidateOnStart();

builder.Services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<DownstreamServicesOptions>, DownstreamServicesOptionsValidator>();
builder.Services.AddHttpClient("order-service", (serviceProvider, client) =>
{
	DownstreamServicesOptions options = serviceProvider
		.GetRequiredService<Microsoft.Extensions.Options.IOptions<DownstreamServicesOptions>>()
		.Value;
	client.BaseAddress = new Uri(options.OrderServiceBaseUrl);
});
builder.Services.AddHttpClient("read-model-service", (serviceProvider, client) =>
{
	DownstreamServicesOptions options = serviceProvider
		.GetRequiredService<Microsoft.Extensions.Options.IOptions<DownstreamServicesOptions>>()
		.Value;
	client.BaseAddress = new Uri(options.ReadModelServiceBaseUrl);
});
builder.Services.AddHttpClient("notification-service", (serviceProvider, client) =>
{
	DownstreamServicesOptions options = serviceProvider
		.GetRequiredService<Microsoft.Extensions.Options.IOptions<DownstreamServicesOptions>>()
		.Value;
	client.BaseAddress = new Uri(options.NotificationServiceBaseUrl);
});
builder.Services.AddHttpClient("recommendation-service", (serviceProvider, client) =>
{
	DownstreamServicesOptions options = serviceProvider
		.GetRequiredService<Microsoft.Extensions.Options.IOptions<DownstreamServicesOptions>>()
		.Value;
	client.BaseAddress = new Uri(options.RecommendationServiceBaseUrl);
});

var app = builder.Build();

app.UseExceptionHandler();
app.MapHealthChecks("/health");

app.MapPost("/orders", (HttpContext context, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
	=> ProxyAsync(context, httpClientFactory.CreateClient("order-service"), "/orders", cancellationToken));

app.MapPost("/orders/{orderId}/confirm", (HttpContext context, string orderId, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
	=> ProxyAsync(context, httpClientFactory.CreateClient("order-service"), $"/orders/{orderId}/confirm", cancellationToken));

app.MapGet("/users/{userId}/orders", (HttpContext context, string userId, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
	=> ProxyAsync(context, httpClientFactory.CreateClient("read-model-service"), $"/users/{userId}/orders", cancellationToken));

app.MapGet("/users/{userId}/orders/{orderId}", (HttpContext context, string userId, string orderId, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
	=> ProxyAsync(context, httpClientFactory.CreateClient("read-model-service"), $"/users/{userId}/orders/{orderId}", cancellationToken));

app.MapGet("/users/{userId}/notifications", (HttpContext context, string userId, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
	=> ProxyAsync(context, httpClientFactory.CreateClient("notification-service"), $"/users/{userId}/notifications", cancellationToken));

app.MapGet("/users/{userId}/recommendations", (HttpContext context, string userId, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
	=> ProxyAsync(context, httpClientFactory.CreateClient("recommendation-service"), $"/users/{userId}/recommendations", cancellationToken));

app.Run();

static async Task ProxyAsync(HttpContext context, HttpClient client, string relativePath, CancellationToken cancellationToken)
{
	using HttpRequestMessage request = new(new HttpMethod(context.Request.Method), relativePath);

	if (context.Request.ContentLength is > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
	{
		request.Content = new StreamContent(context.Request.Body);

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

	await response.Content.CopyToAsync(context.Response.Body, cancellationToken);
}
