using CosmosBootstrapper.Domain;

namespace CosmosBootstrapper.Infrastructure;

internal interface ICosmosContainerCatalog
{
    IReadOnlyCollection<ContainerDefinition> GetRequiredContainers();
}

internal sealed class CosmosContainerCatalog : ICosmosContainerCatalog
{
    private static readonly IReadOnlyCollection<ContainerDefinition> RequiredContainers =
    [
        new("order-events", "/aggregateId"),
        new("orders-read", "/userId"),
        new("users", "/userId"),
        new("recommendations", "/userId")
    ];

    public IReadOnlyCollection<ContainerDefinition> GetRequiredContainers() => RequiredContainers;
}