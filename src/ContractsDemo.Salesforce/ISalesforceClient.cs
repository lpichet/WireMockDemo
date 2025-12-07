using ContractsDemo.Salesforce.Models;

namespace ContractsDemo.Salesforce;

public interface ISalesforceClient
{
    Task<SalesforceAccount?> GetAccountAsync(string accountId, CancellationToken cancellationToken = default);
    Task<SalesforceContact?> GetContactAsync(string contactId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalesforceContact>> GetContactsByAccountAsync(string accountId, CancellationToken cancellationToken = default);
    Task<ContractValidationResponse> ValidateContractAsync(ContractValidationRequest request, CancellationToken cancellationToken = default);
    Task<bool> NotifyContractSignedAsync(string accountId, string contractId, CancellationToken cancellationToken = default);
}
