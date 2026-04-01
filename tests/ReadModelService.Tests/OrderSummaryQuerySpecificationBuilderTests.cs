using ReadModelService.Contracts;
using ReadModelService.Infrastructure;
using Xunit;

namespace ReadModelService.Tests;

public sealed class OrderSummaryQuerySpecificationBuilderTests
{
    [Fact]
    public void Build_without_filters_keeps_partition_scoped_sorted_query()
    {
        OrderSummaryQuery query = new(
            "user-42",
            null,
            null,
            null,
            ListOrderSummariesRequest.DefaultPageSize,
            null);

        OrderSummaryQuerySpecification specification = OrderSummaryQuerySpecificationBuilder.Build(query);

        Assert.Equal(
            "SELECT * FROM c WHERE c.documentType = @documentType AND c.userId = @userId ORDER BY c.lastUpdatedAtUtc DESC",
            specification.QueryText);
        Assert.Equal("user-42", specification.Parameters["@userId"]);
        Assert.Equal("order-summary", specification.Parameters["@documentType"]);
        Assert.Equal(ListOrderSummariesRequest.DefaultPageSize, specification.PageSize);
        Assert.Null(specification.ContinuationToken);
    }

    [Fact]
    public void Build_with_filters_adds_status_and_time_window_predicates()
    {
        DateTime updatedAfterUtc = new(2026, 3, 30, 10, 0, 0, DateTimeKind.Utc);
        DateTime updatedBeforeUtc = new(2026, 3, 31, 10, 0, 0, DateTimeKind.Utc);
        OrderSummaryQuery query = new(
            "user-42",
            "Confirmed",
            updatedAfterUtc,
            updatedBeforeUtc,
            10,
            "next-page-token");

        OrderSummaryQuerySpecification specification = OrderSummaryQuerySpecificationBuilder.Build(query);

        Assert.Contains("c.status = @status", specification.QueryText, StringComparison.Ordinal);
        Assert.Contains("c.lastUpdatedAtUtc >= @updatedAfterUtc", specification.QueryText, StringComparison.Ordinal);
        Assert.Contains("c.lastUpdatedAtUtc <= @updatedBeforeUtc", specification.QueryText, StringComparison.Ordinal);
        Assert.Equal("Confirmed", specification.Parameters["@status"]);
        Assert.Equal(updatedAfterUtc, specification.Parameters["@updatedAfterUtc"]);
        Assert.Equal(updatedBeforeUtc, specification.Parameters["@updatedBeforeUtc"]);
        Assert.Equal("next-page-token", specification.ContinuationToken);
    }

    [Theory]
    [InlineData("pending", "Pending")]
    [InlineData("CONFIRMED", "Confirmed")]
    public void TryNormalizeStatus_accepts_supported_values_case_insensitively(string input, string expected)
    {
        bool isValid = ListOrderSummariesRequest.TryNormalizeStatus(input, out string? normalizedStatus);

        Assert.True(isValid);
        Assert.Equal(expected, normalizedStatus);
    }

    [Fact]
    public void TryNormalizeStatus_rejects_unknown_value()
    {
        bool isValid = ListOrderSummariesRequest.TryNormalizeStatus("Cancelled", out string? normalizedStatus);

        Assert.False(isValid);
        Assert.Null(normalizedStatus);
    }
}