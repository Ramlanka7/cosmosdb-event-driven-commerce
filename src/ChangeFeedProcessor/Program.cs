var builder = Host.CreateApplicationBuilder(args);

builder.Services
	.AddOptions<ChangeFeedProcessor.Configuration.CosmosDbOptions>()
	.Bind(builder.Configuration.GetSection(ChangeFeedProcessor.Configuration.CosmosDbOptions.SectionName))
	.ValidateOnStart();

builder.Services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<ChangeFeedProcessor.Configuration.CosmosDbOptions>, ChangeFeedProcessor.Configuration.CosmosDbOptionsValidator>();
builder.Services.AddSingleton(new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
builder.Services.AddSingleton(ChangeFeedProcessor.Infrastructure.CosmosClientFactory.Create);
builder.Services.AddSingleton<ChangeFeedProcessor.Services.IOrderSummaryProjector, ChangeFeedProcessor.Services.OrderSummaryProjector>();
builder.Services.AddHostedService<ChangeFeedProcessor.Services.OrderEventsProjectionWorker>();

var host = builder.Build();
await host.RunAsync();
