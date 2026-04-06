using Commerce.Eventing.Infrastructure;
using Commerce.Eventing.ReadModels;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

string cosmosEndpoint = builder.Configuration["CosmosDb:Endpoint"] ?? "";
string cosmosKey = builder.Configuration["CosmosDb:Key"] ?? "";
string databaseName = builder.Configuration["CosmosDb:DatabaseName"] ?? "commerce-platform";

if (!string.IsNullOrWhiteSpace(cosmosEndpoint) && !string.IsNullOrWhiteSpace(cosmosKey))
{
    builder.Services.AddSingleton(_ =>
        new CosmosClient(cosmosEndpoint, cosmosKey, new CosmosClientOptions
        {
            HttpClientFactory = () =>
            {
                HttpMessageHandler handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };
                return new HttpClient(handler);
            },
            ConnectionMode = ConnectionMode.Gateway,
            SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            }
        }));
}

builder.Services.AddHttpClient("order-service", client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:OrderServiceBaseUrl"] ?? "http://localhost:5080"));
builder.Services.AddHttpClient("read-model-service", client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:ReadModelServiceBaseUrl"] ?? "http://localhost:5081"));
builder.Services.AddHttpClient("notification-service", client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:NotificationServiceBaseUrl"] ?? "http://localhost:5082"));
builder.Services.AddHttpClient("recommendation-service", client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:RecommendationServiceBaseUrl"] ?? "http://localhost:5083"));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapHealthChecks("/health");

// --- Change Feed Status API ---
// Returns recent items from orders-read, notifications, recommendations, and change-feed-failures

app.MapGet("/api/changefeed/status", async (CosmosClient cosmos) =>
{
    Database db = cosmos.GetDatabase(databaseName);

    var result = new Dictionary<string, object>();

    // Order summaries from orders-read
    result["orderSummaries"] = (await QueryContainerAsync<OrderSummaryDocument>(db, "orders-read", "SELECT TOP 100 * FROM c"))
        .OrderByDescending(item => item.lastUpdatedAtUtc)
        .Take(20)
        .ToArray();

    // Notifications
    result["notifications"] = (await QueryContainerAsync<NotificationDocument>(db, "notifications", "SELECT TOP 100 * FROM c"))
        .OrderByDescending(item => item.occurredAtUtc)
        .Take(20)
        .ToArray();

    // Recommendations
    result["recommendations"] = (await QueryContainerAsync<RecommendationDocument>(db, "recommendations", "SELECT TOP 100 * FROM c"))
        .OrderByDescending(item => item.lastRefreshedAtUtc)
        .Take(20)
        .ToArray();

    // Change feed failures
    result["failures"] = (await QueryContainerAsync<ChangeFeedFailureDocument>(db, "change-feed-failures", "SELECT TOP 100 * FROM c"))
        .OrderByDescending(item => item.lastFailedAtUtc)
        .Take(20)
        .ToArray();

    return Results.Ok(result);
});

// Container-level document counts
app.MapGet("/api/changefeed/counts", async (CosmosClient cosmos) =>
{
    Database db = cosmos.GetDatabase(databaseName);
    string[] containers = ["order-events", "orders-read", "notifications", "recommendations", "change-feed-failures"];

    var counts = new Dictionary<string, int>();
    foreach (string name in containers)
    {
        try
        {
            Container container = db.GetContainer(name);
            var items = new List<long>();
            using FeedIterator<long> feed = container.GetItemQueryIterator<long>(
                "SELECT VALUE COUNT(1) FROM c");
            while (feed.HasMoreResults)
            {
                FeedResponse<long> response = await feed.ReadNextAsync();
                items.AddRange(response);
            }

            if (items.Count == 0)
            {
                counts[name] = 0;
            }
            else if (items[0] <= int.MaxValue && items[0] >= int.MinValue)
            {
                counts[name] = (int)items[0];
            }
            else
            {
                counts[name] = -1;
            }
        }
        catch
        {
            counts[name] = -1;
        }
    }

    return Results.Ok(counts);
});

// Raw events for an order
app.MapGet("/api/events/{orderId}", async (string orderId, CosmosClient cosmos) =>
{
    Database db = cosmos.GetDatabase(databaseName);
    Container container = db.GetContainer("order-events");

    var items = new List<CosmosEventDocument>();
    using FeedIterator<CosmosEventDocument> feed = container.GetItemQueryIterator<CosmosEventDocument>(
        new QueryDefinition("SELECT * FROM c WHERE c.aggregateId = @id ORDER BY c.sequenceNumber")
            .WithParameter("@id", orderId));
    while (feed.HasMoreResults)
    {
        FeedResponse<CosmosEventDocument> response = await feed.ReadNextAsync();
        items.AddRange(response);
    }

    return Results.Text(JsonConvert.SerializeObject(items), "application/json");
});

app.Run();

static async Task<List<TDocument>> QueryContainerAsync<TDocument>(Database db, string containerName, string sql)
{
    try
    {
        Container container = db.GetContainer(containerName);
        var items = new List<TDocument>();
        using FeedIterator<TDocument> feed = container.GetItemQueryIterator<TDocument>(sql,
            requestOptions: new QueryRequestOptions { MaxItemCount = 20 });
        while (feed.HasMoreResults)
        {
            FeedResponse<TDocument> response = await feed.ReadNextAsync();
            items.AddRange(response);
        }

        return items;
    }
    catch
    {
        return [];
    }
}
