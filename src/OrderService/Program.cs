using System.Text.Json;
using OrderService.Configuration;
using OrderService.Contracts;
using OrderService.Domain;
using OrderService.Infrastructure;
using OrderService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

builder.Services
    .AddOptions<CosmosDbOptions>()
    .Bind(builder.Configuration.GetSection(CosmosDbOptions.SectionName))
    .ValidateOnStart();

builder.Services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<CosmosDbOptions>, CosmosDbOptionsValidator>();
builder.Services.AddSingleton(new JsonSerializerOptions(JsonSerializerDefaults.Web));
builder.Services.AddSingleton(CreateCosmosClient);
builder.Services.AddSingleton<IOrderEventDocumentMapper, OrderEventDocumentMapper>();
builder.Services.AddSingleton<IOrderEventStore, CosmosOrderEventStore>();
builder.Services.AddSingleton<IOrderCommandService, OrderCommandService>();

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

RouteGroupBuilder orders = app.MapGroup("/orders");

orders.MapPost("/", async Task<IResult> (
    CreateOrderRequest request,
    IOrderCommandService orderCommandService,
    CancellationToken cancellationToken) =>
{
    try
    {
        OrderCommandResponse response = await orderCommandService.CreateOrderAsync(request, cancellationToken);
        return TypedResults.Created($"/orders/{response.OrderId}/events", response);
    }
    catch (OrderDomainException exception)
    {
        return TypedResults.BadRequest(new ProblemDetails
        {
            Title = "Invalid create-order command.",
            Detail = exception.Message,
            Status = StatusCodes.Status400BadRequest
        });
    }
});

orders.MapPost("/{orderId}/confirm", async Task<IResult> (
    string orderId,
    ConfirmOrderRequest? request,
    IOrderCommandService orderCommandService,
    CancellationToken cancellationToken) =>
{
    try
    {
        OrderCommandResponse response = await orderCommandService.ConfirmOrderAsync(orderId, request, cancellationToken);
        return TypedResults.Ok(response);
    }
    catch (KeyNotFoundException exception)
    {
        return TypedResults.NotFound(new ProblemDetails
        {
            Title = "Order not found.",
            Detail = exception.Message,
            Status = StatusCodes.Status404NotFound
        });
    }
    catch (ConcurrentStreamWriteException exception)
    {
        return TypedResults.Conflict(new ProblemDetails
        {
            Title = "Concurrent stream write detected.",
            Detail = exception.Message,
            Status = StatusCodes.Status409Conflict
        });
    }
    catch (OrderDomainException exception)
    {
        return TypedResults.Conflict(new ProblemDetails
        {
            Title = "Invalid confirm-order command.",
            Detail = exception.Message,
            Status = StatusCodes.Status409Conflict
        });
    }
});

orders.MapGet("/{orderId}/events", async Task<IResult> (
    string orderId,
    IOrderCommandService orderCommandService,
    CancellationToken cancellationToken) =>
{
    OrderStream stream = await orderCommandService.GetStreamAsync(orderId, cancellationToken);

    if (stream.Version == 0)
    {
        return TypedResults.NotFound(new ProblemDetails
        {
            Title = "Order not found.",
            Detail = $"No event stream exists for order '{orderId}'.",
            Status = StatusCodes.Status404NotFound
        });
    }

    OrderStreamResponse response = new(
        stream.AggregateId,
        stream.Version,
        stream.Events.Select(orderEvent => new OrderEventResponse(
            orderEvent.SequenceNumber,
            orderEvent.EventType,
            orderEvent.EventVersion,
            orderEvent.OccurredAtUtc,
            orderEvent.CorrelationId,
            orderEvent.CausationId,
            orderEvent.Payload)).ToArray());

    return TypedResults.Ok(response);
});

app.Run();

static CosmosClient CreateCosmosClient(IServiceProvider serviceProvider)
{
    CosmosDbOptions options = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<CosmosDbOptions>>()
        .Value;

    CosmosClientOptions clientOptions = new()
    {
        ApplicationName = "cosmosdb-event-driven-commerce-order-service",
        ConnectionMode = ConnectionMode.Direct,
        MaxRetryAttemptsOnRateLimitedRequests = 9,
        MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(30)
    };

    if (options.PreferredRegions.Count > 0)
    {
        clientOptions.ApplicationPreferredRegions = options.PreferredRegions;
    }
    
        if (ShouldAllowInsecureCertificate(options.Endpoint))
        {
            clientOptions.ConnectionMode = ConnectionMode.Gateway;
            clientOptions.HttpClientFactory = () => new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
        }

    return new CosmosClient(options.Endpoint, options.Key, clientOptions);

    static bool ShouldAllowInsecureCertificate(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? endpointUri))
        {
            return false;
        }

        return endpointUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
               || endpointUri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
             || endpointUri.Host.Equals("host.docker.internal", StringComparison.OrdinalIgnoreCase)
             || endpointUri.Host.Equals("cosmos-emulator", StringComparison.OrdinalIgnoreCase);
    }
}