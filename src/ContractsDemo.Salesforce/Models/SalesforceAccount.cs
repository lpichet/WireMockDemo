namespace ContractsDemo.Salesforce.Models;

public record SalesforceAccount
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Industry { get; init; }
    public string? Type { get; init; }
    public string? BillingCity { get; init; }
    public string? BillingCountry { get; init; }
    public string? Phone { get; init; }
    public string? Website { get; init; }
    public decimal? AnnualRevenue { get; init; }
    public int? NumberOfEmployees { get; init; }
    public bool IsActive { get; init; } = true;
}
