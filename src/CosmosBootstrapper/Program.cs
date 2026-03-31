using CosmosBootstrapper.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddCosmosBootstrapper(builder.Configuration);

try
{
    using IHost host = builder.Build();
    await host.RunAsync();
    return Environment.ExitCode;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Cosmos bootstrapper failed to start: {exception.Message}");
    return 1;
}
