namespace ContractsDemo.Api.Data;

public class Contract
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required decimal Value { get; set; }
    public required string ContractType { get; set; }
    public ContractStatus Status { get; set; } = ContractStatus.Draft;

    // Salesforce references
    public required string SalesforceAccountId { get; set; }
    public required string SalesforceContactId { get; set; }

    // Cached Salesforce data
    public string? AccountName { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }

    // Validation from Salesforce
    public bool? IsValidated { get; set; }
    public string? ValidationMessage { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? SignedAt { get; set; }
    public string? SignedBy { get; set; }
}

public enum ContractStatus
{
    Draft,
    PendingValidation,
    Validated,
    PendingSignature,
    Signed,
    Rejected,
    Cancelled
}
