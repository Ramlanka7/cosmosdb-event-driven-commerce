namespace ReadModelService.Infrastructure;

internal sealed record OrderSummaryQuerySpecification(
    string QueryText,
    IReadOnlyDictionary<string, object> Parameters,
    string UserId,
    int PageSize,
    string? ContinuationToken);

internal static class OrderSummaryQuerySpecificationBuilder
{
    public static OrderSummaryQuerySpecification Build(OrderSummaryQuery query)
    {
        List<string> predicates =
        [
            "c.documentType = @documentType",
            "c.userId = @userId"
        ];

        Dictionary<string, object> parameters = new(StringComparer.Ordinal)
        {
            ["@documentType"] = "order-summary",
            ["@userId"] = query.UserId
        };

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            predicates.Add("c.status = @status");
            parameters["@status"] = query.Status;
        }

        if (query.UpdatedAfterUtc is not null)
        {
            predicates.Add("c.lastUpdatedAtUtc >= @updatedAfterUtc");
            parameters["@updatedAfterUtc"] = query.UpdatedAfterUtc.Value;
        }

        if (query.UpdatedBeforeUtc is not null)
        {
            predicates.Add("c.lastUpdatedAtUtc <= @updatedBeforeUtc");
            parameters["@updatedBeforeUtc"] = query.UpdatedBeforeUtc.Value;
        }

        string queryText = $"SELECT * FROM c WHERE {string.Join(" AND ", predicates)} ORDER BY c.lastUpdatedAtUtc DESC";

        return new OrderSummaryQuerySpecification(
            queryText,
            parameters,
            query.UserId,
            query.PageSize,
            query.ContinuationToken);
    }
}