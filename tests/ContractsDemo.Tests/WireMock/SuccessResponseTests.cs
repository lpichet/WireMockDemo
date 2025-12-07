using System.Net;
using System.Net.Http.Json;
using ContractsDemo.Api.Data;
using ContractsDemo.Api.Models;
using ContractsDemo.Tests.Fixtures;
using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace ContractsDemo.Tests.WireMock;

/// <summary>
/// Demonstrates WireMock SUCCESS RESPONSES (.RespondWith(200))
///
/// These tests show how to mock successful API responses from Salesforce.
/// This is the most common use case for WireMock - simulating successful external API calls.
/// </summary>
[Collection("WireMock")]
public class SuccessResponseTests : IDisposable
{
    private readonly WireMockFixture _wireMock;
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SuccessResponseTests(WireMockFixture wireMock)
    {
        _wireMock = wireMock;
        _wireMock.Reset();
        _factory = new TestWebApplicationFactory(_wireMock.BaseUrl);
        _client = _factory.CreateClient();
    }

    /// <summary>
    /// Basic success response - returns a simple 200 OK with JSON body
    /// </summary>
    [Fact]
    public async Task CreateContract_WithValidSalesforceData_ReturnsCreatedContract()
    {
        // Arrange - Setup WireMock to return successful responses
        const string accountId = "001XX000003DGb2";
        const string contactId = "003XX000004TmiD";

        // Mock GET Account endpoint - returns 200 with account data
        _wireMock.Server
            .Given(Request.Create()
                .WithPath($"/services/data/v59.0/sobjects/Account/{accountId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    id = accountId,
                    name = "Acme Corporation",
                    industry = "Technology",
                    type = "Customer",
                    billingCity = "San Francisco",
                    billingCountry = "USA",
                    phone = "+1-555-1234",
                    website = "https://acme.com",
                    annualRevenue = 10000000,
                    numberOfEmployees = 500,
                    isActive = true
                }));

        // Mock GET Contact endpoint - returns 200 with contact data
        _wireMock.Server
            .Given(Request.Create()
                .WithPath($"/services/data/v59.0/sobjects/Contact/{contactId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    id = contactId,
                    accountId = accountId,
                    firstName = "John",
                    lastName = "Doe",
                    email = "john.doe@acme.com",
                    phone = "+1-555-5678",
                    title = "VP of Sales",
                    department = "Sales"
                }));

        var createRequest = new CreateContractRequest(
            Title: "Enterprise License Agreement",
            Description: "Annual enterprise software license",
            Value: 50000m,
            ContractType: "Enterprise",
            SalesforceAccountId: accountId,
            SalesforceContactId: contactId);

        // Act
        var response = await _client.PostAsJsonAsync("/api/contracts", createRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var contract = await response.Content.ReadFromJsonAsync<ContractResponse>();
        contract.Should().NotBeNull();
        contract!.Title.Should().Be("Enterprise License Agreement");
        contract.AccountName.Should().Be("Acme Corporation");
        contract.ContactName.Should().Be("John Doe");
        contract.ContactEmail.Should().Be("john.doe@acme.com");
        contract.Status.Should().Be(ContractStatus.Draft);
    }

    /// <summary>
    /// Success response with validation approval
    /// </summary>
    [Fact]
    public async Task ValidateContract_WhenSalesforceApproves_ReturnsValidatedContract()
    {
        // Arrange - First create a contract
        const string accountId = "001XX000003DGb3";
        const string contactId = "003XX000004TmiE";

        SetupBasicAccountAndContact(accountId, contactId);

        // Create contract first
        var createRequest = new CreateContractRequest(
            Title: "Standard License",
            Description: "Standard software license agreement",
            Value: 10000m,
            ContractType: "Standard",
            SalesforceAccountId: accountId,
            SalesforceContactId: contactId);

        var createResponse = await _client.PostAsJsonAsync("/api/contracts", createRequest);
        var createdContract = await createResponse.Content.ReadFromJsonAsync<ContractResponse>();

        // Setup validation endpoint to return approval
        _wireMock.Server
            .Given(Request.Create()
                .WithPath("/services/data/v59.0/contract/validate")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    isValid = true,
                    validationMessage = "Contract approved - within credit limits",
                    approvalStatus = "Approved",
                    creditLimit = 100000m,
                    requiredApprovers = new[] { "manager@acme.com" }
                }));

        // Act
        var validateResponse = await _client.PostAsync($"/api/contracts/{createdContract!.Id}/validate", null);

        // Assert
        validateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var validatedContract = await validateResponse.Content.ReadFromJsonAsync<ContractResponse>();
        validatedContract.Should().NotBeNull();
        validatedContract!.IsValidated.Should().BeTrue();
        validatedContract.ValidationMessage.Should().Be("Contract approved - within credit limits");
        validatedContract.Status.Should().Be(ContractStatus.Validated);
    }

    /// <summary>
    /// Success response that returns rejection (still 200 OK, but validation fails)
    /// </summary>
    [Fact]
    public async Task ValidateContract_WhenSalesforceRejects_ReturnsRejectedContract()
    {
        // Arrange
        const string accountId = "001XX000003DGb4";
        const string contactId = "003XX000004TmiF";

        SetupBasicAccountAndContact(accountId, contactId);

        var createRequest = new CreateContractRequest(
            Title: "Large Enterprise Deal",
            Description: "Very large contract requiring approval",
            Value: 5000000m, // Very large value
            ContractType: "Enterprise",
            SalesforceAccountId: accountId,
            SalesforceContactId: contactId);

        var createResponse = await _client.PostAsJsonAsync("/api/contracts", createRequest);
        var createdContract = await createResponse.Content.ReadFromJsonAsync<ContractResponse>();

        // Setup validation to return rejection (200 OK with isValid=false)
        _wireMock.Server
            .Given(Request.Create()
                .WithPath("/services/data/v59.0/contract/validate")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    isValid = false,
                    validationMessage = "Contract value exceeds account credit limit. Requires executive approval.",
                    approvalStatus = "Rejected",
                    creditLimit = 100000m,
                    requiredApprovers = new[] { "cfo@acme.com", "ceo@acme.com" }
                }));

        // Act
        var validateResponse = await _client.PostAsync($"/api/contracts/{createdContract!.Id}/validate", null);

        // Assert
        validateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var validatedContract = await validateResponse.Content.ReadFromJsonAsync<ContractResponse>();
        validatedContract!.IsValidated.Should().BeFalse();
        validatedContract.Status.Should().Be(ContractStatus.Rejected);
        validatedContract.ValidationMessage.Should().Contain("executive approval");
    }

    private void SetupBasicAccountAndContact(string accountId, string contactId)
    {
        _wireMock.Server
            .Given(Request.Create()
                .WithPath($"/services/data/v59.0/sobjects/Account/{accountId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    id = accountId,
                    name = "Test Company",
                    isActive = true
                }));

        _wireMock.Server
            .Given(Request.Create()
                .WithPath($"/services/data/v59.0/sobjects/Contact/{contactId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    id = contactId,
                    accountId = accountId,
                    firstName = "Test",
                    lastName = "User",
                    email = "test@company.com"
                }));
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }
}
