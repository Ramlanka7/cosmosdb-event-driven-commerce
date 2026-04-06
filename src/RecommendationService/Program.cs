using Commerce.Eventing.ReadModels;
using RecommendationService.Configuration;
using RecommendationService.Contracts;
using RecommendationService.Infrastructure;
using RecommendationService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services
	.AddOptions<CosmosDbOptions>()
	.Bind(builder.Configuration.GetSection(CosmosDbOptions.SectionName))
	.ValidateOnStart();

builder.Services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<CosmosDbOptions>, CosmosDbOptionsValidator>();
builder.Services.AddSingleton(CosmosClientFactory.Create);
builder.Services.AddSingleton<IRecommendationStore, CosmosRecommendationStore>();

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
