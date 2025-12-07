using System.Net.Http.Json;
using System.Text.Json;
using ContractsDemo.Salesforce.Models;
using Microsoft.Extensions.Options;

namespace ContractsDemo.Salesforce;

public class SalesforceClient : ISalesforceClient
{
    private readonly HttpClient _httpClient;
    private readonly SalesforceClientOptions _options;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public SalesforceClient(HttpClient httpClient, IOptions<SalesforceClientOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<SalesforceAccount?> GetAccountAsync(string accountId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"/services/data/{_options.ApiVersion}/sobjects/Account/{accountId}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();
        }

        return await response.Content.ReadFromJsonAsync<SalesforceAccount>(JsonOptions, cancellationToken);
    }

    public async Task<SalesforceContact?> GetContactAsync(string contactId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"/services/data/{_options.ApiVersion}/sobjects/Contact/{contactId}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();
        }

        return await response.Content.ReadFromJsonAsync<SalesforceContact>(JsonOptions, cancellationToken);
    }

    public async Task<IReadOnlyList<SalesforceContact>> GetContactsByAccountAsync(string accountId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"/services/data/{_options.ApiVersion}/sobjects/Account/{accountId}/Contacts",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ContactsResponse>(JsonOptions, cancellationToken);
        return result?.Records ?? [];
    }

    public async Task<ContractValidationResponse> ValidateContractAsync(ContractValidationRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"/services/data/{_options.ApiVersion}/contract/validate",
            request,
            JsonOptions,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ContractValidationResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Invalid response from Salesforce API");
    }

    public async Task<bool> NotifyContractSignedAsync(string accountId, string contractId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"/services/data/{_options.ApiVersion}/contract/notify",
            new { AccountId = accountId, ContractId = contractId },
            JsonOptions,
            cancellationToken);

        return response.IsSuccessStatusCode;
    }

    private record ContactsResponse(IReadOnlyList<SalesforceContact> Records);
}
