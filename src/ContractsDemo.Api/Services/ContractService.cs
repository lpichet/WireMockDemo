using ContractsDemo.Api.Data;
using ContractsDemo.Api.Models;
using ContractsDemo.Salesforce;
using ContractsDemo.Salesforce.Models;
using Microsoft.EntityFrameworkCore;

namespace ContractsDemo.Api.Services;

public interface IContractService
{
    Task<ContractResponse> CreateContractAsync(CreateContractRequest request, CancellationToken cancellationToken = default);
    Task<ContractResponse?> GetContractAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContractResponse>> GetContractsAsync(CancellationToken cancellationToken = default);
    Task<ContractResponse?> UpdateContractAsync(Guid id, UpdateContractRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteContractAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ContractResponse?> ValidateContractAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ContractResponse?> SignContractAsync(Guid id, SignContractRequest request, CancellationToken cancellationToken = default);
}

public class ContractService : IContractService
{
    private readonly ContractsDbContext _dbContext;
    private readonly ISalesforceClient _salesforceClient;
    private readonly ILogger<ContractService> _logger;

    public ContractService(
        ContractsDbContext dbContext,
        ISalesforceClient salesforceClient,
        ILogger<ContractService> logger)
    {
        _dbContext = dbContext;
        _salesforceClient = salesforceClient;
        _logger = logger;
    }

    public async Task<ContractResponse> CreateContractAsync(CreateContractRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating contract for account {AccountId}", request.SalesforceAccountId);

        // Fetch account and contact info from Salesforce
        var account = await _salesforceClient.GetAccountAsync(request.SalesforceAccountId, cancellationToken);
        var contact = await _salesforceClient.GetContactAsync(request.SalesforceContactId, cancellationToken);

        if (account is null)
        {
            throw new InvalidOperationException($"Salesforce account {request.SalesforceAccountId} not found");
        }

        if (contact is null)
        {
            throw new InvalidOperationException($"Salesforce contact {request.SalesforceContactId} not found");
        }

        var contract = new Contract
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            Value = request.Value,
            ContractType = request.ContractType,
            SalesforceAccountId = request.SalesforceAccountId,
            SalesforceContactId = request.SalesforceContactId,
            AccountName = account.Name,
            ContactName = $"{contact.FirstName} {contact.LastName}",
            ContactEmail = contact.Email,
            Status = ContractStatus.Draft
        };

        _dbContext.Contracts.Add(contract);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Contract {ContractId} created successfully", contract.Id);

        return contract.ToResponse();
    }

    public async Task<ContractResponse?> GetContractAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var contract = await _dbContext.Contracts.FindAsync([id], cancellationToken);
        return contract?.ToResponse();
    }

    public async Task<IReadOnlyList<ContractResponse>> GetContractsAsync(CancellationToken cancellationToken = default)
    {
        var contracts = await _dbContext.Contracts
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        return contracts.Select(c => c.ToResponse()).ToList();
    }

    public async Task<ContractResponse?> UpdateContractAsync(Guid id, UpdateContractRequest request, CancellationToken cancellationToken = default)
    {
        var contract = await _dbContext.Contracts.FindAsync([id], cancellationToken);

        if (contract is null)
            return null;

        if (contract.Status == ContractStatus.Signed)
        {
            throw new InvalidOperationException("Cannot update a signed contract");
        }

        contract.Title = request.Title;
        contract.Description = request.Description;
        contract.Value = request.Value;
        contract.ContractType = request.ContractType;
        contract.UpdatedAt = DateTime.UtcNow;

        // Reset validation if contract was validated
        if (contract.Status == ContractStatus.Validated)
        {
            contract.Status = ContractStatus.Draft;
            contract.IsValidated = null;
            contract.ValidationMessage = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return contract.ToResponse();
    }

    public async Task<bool> DeleteContractAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var contract = await _dbContext.Contracts.FindAsync([id], cancellationToken);

        if (contract is null)
            return false;

        if (contract.Status == ContractStatus.Signed)
        {
            throw new InvalidOperationException("Cannot delete a signed contract");
        }

        _dbContext.Contracts.Remove(contract);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<ContractResponse?> ValidateContractAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var contract = await _dbContext.Contracts.FindAsync([id], cancellationToken);

        if (contract is null)
            return null;

        _logger.LogInformation("Validating contract {ContractId} with Salesforce", id);

        contract.Status = ContractStatus.PendingValidation;

        // Call Salesforce to validate the contract
        var validationRequest = new ContractValidationRequest
        {
            AccountId = contract.SalesforceAccountId,
            ContactId = contract.SalesforceContactId,
            ContractValue = contract.Value,
            ContractType = contract.ContractType
        };

        var validationResponse = await _salesforceClient.ValidateContractAsync(validationRequest, cancellationToken);

        contract.IsValidated = validationResponse.IsValid;
        contract.ValidationMessage = validationResponse.ValidationMessage;
        contract.Status = validationResponse.IsValid ? ContractStatus.Validated : ContractStatus.Rejected;
        contract.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Contract {ContractId} validation result: {IsValid}", id, validationResponse.IsValid);

        return contract.ToResponse();
    }

    public async Task<ContractResponse?> SignContractAsync(Guid id, SignContractRequest request, CancellationToken cancellationToken = default)
    {
        var contract = await _dbContext.Contracts.FindAsync([id], cancellationToken);

        if (contract is null)
            return null;

        if (contract.Status != ContractStatus.Validated)
        {
            throw new InvalidOperationException("Contract must be validated before signing");
        }

        _logger.LogInformation("Signing contract {ContractId}", id);

        contract.Status = ContractStatus.PendingSignature;

        // Notify Salesforce about the signed contract
        var notified = await _salesforceClient.NotifyContractSignedAsync(
            contract.SalesforceAccountId,
            contract.Id.ToString(),
            cancellationToken);

        if (!notified)
        {
            _logger.LogWarning("Failed to notify Salesforce about signed contract {ContractId}", id);
        }

        contract.Status = ContractStatus.Signed;
        contract.SignedAt = DateTime.UtcNow;
        contract.SignedBy = request.SignedBy;
        contract.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Contract {ContractId} signed by {SignedBy}", id, request.SignedBy);

        return contract.ToResponse();
    }
}
