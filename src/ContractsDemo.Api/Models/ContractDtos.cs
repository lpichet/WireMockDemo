using ContractsDemo.Api.Data;

namespace ContractsDemo.Api.Models;

public record CreateContractRequest(
    string Title,
    string Description,
    decimal Value,
    string ContractType,
    string SalesforceAccountId,
    string SalesforceContactId);

public record UpdateContractRequest(
    string Title,
    string Description,
    decimal Value,
    string ContractType);

public record SignContractRequest(
    string SignedBy);

public record ContractResponse(
    Guid Id,
    string Title,
    string Description,
    decimal Value,
    string ContractType,
    ContractStatus Status,
    string SalesforceAccountId,
    string SalesforceContactId,
    string? AccountName,
    string? ContactName,
    string? ContactEmail,
    bool? IsValidated,
    string? ValidationMessage,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? SignedAt,
    string? SignedBy);

public static class ContractExtensions
{
    public static ContractResponse ToResponse(this Contract contract) => new(
        contract.Id,
        contract.Title,
        contract.Description,
        contract.Value,
        contract.ContractType,
        contract.Status,
        contract.SalesforceAccountId,
        contract.SalesforceContactId,
        contract.AccountName,
        contract.ContactName,
        contract.ContactEmail,
        contract.IsValidated,
        contract.ValidationMessage,
        contract.CreatedAt,
        contract.UpdatedAt,
        contract.SignedAt,
        contract.SignedBy);
}
