namespace ContractsDemo.Salesforce.Models;

public record SalesforceContact
{
    public required string Id { get; init; }
    public required string AccountId { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Title { get; init; }
    public string? Department { get; init; }
}
