using Commerce.Eventing.ReadModels;
using RecommendationService.Configuration;
using RecommendationService.Contracts;
using RecommendationService.Infrastructure;
using RecommendationService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services
	.AddOptions<CosmosDbOptions>()
	.Bind(builder.Configuration.GetSection(CosmosDbOptions.SectionName))
	.ValidateOnStart();

builder.Services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<CosmosDbOptions>, CosmosDbOptionsValidator>();
builder.Services.AddSingleton(new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
builder.Services.AddSingleton(CosmosClientFactory.Create);
builder.Services.AddSingleton<IRecommendationStore, CosmosRecommendationStore>();
builder.Services.AddHostedService<RecommendationFeedWorker>();

var app = builder.Build();

app.UseExceptionHandler();

app.MapGet("/users/{userId}/recommendations", async Task<IResult> (
	string userId,
	IRecommendationStore recommendationStore,
	CancellationToken cancellationToken) =>
{
	RecommendationDocument? document = await recommendationStore.GetAsync(userId, cancellationToken);
	RecommendationResponse response = new(
		userId,
		document?.lastRefreshedAtUtc,
		document?.suggestedSkus.Select(item => new RecommendationItemResponse(item.Sku, item.Score, item.Reason)).ToArray() ?? []);

	return TypedResults.Ok(response);
});

app.Run();
