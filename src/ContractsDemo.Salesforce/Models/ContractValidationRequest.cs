namespace ContractsDemo.Salesforce.Models;

public record ContractValidationRequest
{
    public required string AccountId { get; init; }
    public required string ContactId { get; init; }
    public required decimal ContractValue { get; init; }
    public required string ContractType { get; init; }
}

public record ContractValidationResponse
{
    public bool IsValid { get; init; }
    public string? ValidationMessage { get; init; }
    public string? ApprovalStatus { get; init; }
    public decimal? CreditLimit { get; init; }
    public string[]? RequiredApprovers { get; init; }
}
