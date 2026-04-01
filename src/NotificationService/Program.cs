using Commerce.Eventing.ReadModels;
using Microsoft.AspNetCore.Mvc;
using NotificationService.Configuration;
using NotificationService.Contracts;
using NotificationService.Infrastructure;
using NotificationService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services
	.AddOptions<CosmosDbOptions>()
	.Bind(builder.Configuration.GetSection(CosmosDbOptions.SectionName))
	.ValidateOnStart();

builder.Services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<CosmosDbOptions>, CosmosDbOptionsValidator>();
builder.Services.AddSingleton(new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
builder.Services.AddSingleton(CosmosClientFactory.Create);
builder.Services.AddSingleton<IChangeFeedFailureStore, CosmosChangeFeedFailureStore>();
builder.Services.AddSingleton<INotificationStore, CosmosNotificationStore>();
builder.Services.AddHostedService<NotificationFeedWorker>();

var app = builder.Build();

app.UseExceptionHandler();
app.MapHealthChecks("/health");

app.MapGet("/users/{userId}/notifications", async Task<IResult> (
	string userId,
	INotificationStore notificationStore,
	CancellationToken cancellationToken) =>
{
	IReadOnlyCollection<NotificationDocument> notifications = await notificationStore.ListAsync(userId, cancellationToken);
	IReadOnlyCollection<NotificationResponse> response = notifications
		.Select(document => new NotificationResponse(
			document.orderId,
			document.notificationType,
			document.message,
			document.occurredAtUtc))
		.ToArray();

	return TypedResults.Ok(response);
});

app.Run();
