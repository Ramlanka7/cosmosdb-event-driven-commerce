using Commerce.Eventing.ReadModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using ReadModelService.Configuration;
using ReadModelService.Contracts;
using ReadModelService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services
	.AddOptions<CosmosDbOptions>()
	.Bind(builder.Configuration.GetSection(CosmosDbOptions.SectionName))
	.ValidateOnStart();

builder.Services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<CosmosDbOptions>, CosmosDbOptionsValidator>();
builder.Services.AddSingleton(CosmosClientFactory.Create);
builder.Services.AddSingleton<IOrderSummaryQueryService, CosmosOrderSummaryQueryService>();

var app = builder.Build();

app.UseExceptionHandler();

RouteGroupBuilder users = app.MapGroup("/users/{userId}");

users.MapGet("/orders", async Task<IResult> (
	string userId,
	IOrderSummaryQueryService queryService,
	CancellationToken cancellationToken) =>
{
	IReadOnlyCollection<OrderSummaryDocument> documents = await queryService.ListOrdersAsync(userId, cancellationToken);
	IReadOnlyCollection<OrderSummaryResponse> response = documents.Select(MapResponse).ToArray();
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

static OrderSummaryResponse MapResponse(OrderSummaryDocument document) => new(
	document.orderId,
	document.userId,
	document.status,
	document.totalAmount,
	document.createdAtUtc,
	document.lastUpdatedAtUtc,
	document.items.Select(item => new OrderItemResponse(item.Sku, item.Quantity, item.UnitPrice)).ToArray());
