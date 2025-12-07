using System.Net;
using System.Net.Http.Json;
using ContractsDemo.Api.Models;
using ContractsDemo.Tests.Fixtures;
using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace ContractsDemo.Tests.WireMock;

/// <summary>
/// Demonstrates WireMock FAILURE SCENARIOS (.WithStatusCode(4xx/5xx))
///
/// These tests show how to simulate various HTTP error responses from Salesforce.
/// This is essential for testing error handling and resilience in your application.
/// </summary>
[Collection("WireMock")]
public class FailureScenarioTests : IDisposable
{
    private readonly WireMockFixture _wireMock;
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FailureScenarioTests(WireMockFixture wireMock)
    {
        _wireMock = wireMock;
        _wireMock.Reset();
        _factory = new TestWebApplicationFactory(_wireMock.BaseUrl);
        _client = _factory.CreateClient();
    }

    /// <summary>
    /// 404 Not Found - Account doesn't exist in Salesforce
    /// </summary>
    [Fact]
    public async Task CreateContract_WhenAccountNotFound_ReturnsBadRequest()
    {
        // Arrange
        const string accountId = "001XX000003NOTFOUND";
        const string contactId = "003XX000004TmiD";

        // Mock 404 for account
        _wireMock.Server
            .Given(Request.Create()
                .WithPath($"/services/data/v59.0/sobjects/Account/{accountId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.NotFound)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new[]
                {
                    new
                    {
                        message = "The requested resource does not exist",
                        errorCode = "NOT_FOUND"
                    }
                }));

        var createRequest = new CreateContractRequest(
            Title: "Test Contract",
            Description: "Test Description",
            Value: 10000m,
            ContractType: "Standard",
            SalesforceAccountId: accountId,
            SalesforceContactId: contactId);

        // Act
        var response = await _client.PostAsJsonAsync("/api/contracts", createRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("not found");
    }

    /// <summary>
    /// 401 Unauthorized - Invalid or expired Salesforce token
    /// </summary>
    [Fact]
    public async Task CreateContract_WhenSalesforceUnauthorized_ReturnsBadGateway()
    {
        // Arrange
        const string accountId = "001XX000003DGb2";

        _wireMock.Server
            .Given(Request.Create()
                .WithPath($"/services/data/v59.0/sobjects/Account/{accountId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.Unauthorized)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new[]
                {
                    new
                    {
                        message = "Session expired or invalid",
                        errorCode = "INVALID_SESSION_ID"
                    }
                }));

        var createRequest = new CreateContractRequest(
            Title: "Test Contract",
            Description: "Test",
            Value: 10000m,
            ContractType: "Standard",
            SalesforceAccountId: accountId,
            SalesforceContactId: "003XX000004TmiD");

        // Act
        var response = await _client.PostAsJsonAsync("/api/contracts", createRequest);

        // Assert - Application should handle auth errors gracefully
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.BadGateway,
            HttpStatusCode.InternalServerError);
    }

    /// <summary>
    /// 500 Internal Server Error - Salesforce is having issues
    /// </summary>
    [Fact]
    public async Task CreateContract_WhenSalesforceServerError_ReturnsServerError()
    {
        // Arrange
        const string accountId = "001XX000003DGb2";

        _wireMock.Server
            .Given(Request.Create()
                .WithPath($"/services/data/v59.0/sobjects/Account/{accountId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.InternalServerError)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new[]
                {
                    new
                    {
                        message = "An unexpected error occurred",
                        errorCode = "UNKNOWN_EXCEPTION"
                    }
                }));

        var createRequest = new CreateContractRequest(
            Title: "Test Contract",
            Description: "Test",
            Value: 10000m,
            ContractType: "Standard",
            SalesforceAccountId: accountId,
            SalesforceContactId: "003XX000004TmiD");

        // Act
        var response = await _client.PostAsJsonAsync("/api/contracts", createRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadGateway,
            HttpStatusCode.InternalServerError);
    }

    /// <summary>
    /// 503 Service Unavailable - Salesforce is under maintenance
    /// </summary>
    [Fact]
    public async Task CreateContract_WhenSalesforceUnavailable_HandlesGracefully()
    {
        // Arrange
        const string accountId = "001XX000003DGb2";

        _wireMock.Server
            .Given(Request.Create()
                .WithPath($"/services/data/v59.0/sobjects/Account/{accountId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.ServiceUnavailable)
                .WithHeader("Content-Type", "application/json")
                .WithHeader("Retry-After", "300")
                .WithBodyAsJson(new
                {
                    message = "Service temporarily unavailable. Please try again later.",
                    errorCode = "SERVICE_UNAVAILABLE"
                }));

        var createRequest = new CreateContractRequest(
            Title: "Test Contract",
            Description: "Test",
            Value: 10000m,
            ContractType: "Standard",
            SalesforceAccountId: accountId,
            SalesforceContactId: "003XX000004TmiD");

        // Act
        var response = await _client.PostAsJsonAsync("/api/contracts", createRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadGateway,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.InternalServerError);
    }

    /// <summary>
    /// 429 Too Many Requests - Rate limiting from Salesforce
    /// </summary>
    [Fact]
    public async Task CreateContract_WhenRateLimited_HandlesGracefully()
    {
        // Arrange
        const string accountId = "001XX000003DGb2";

        _wireMock.Server
            .Given(Request.Create()
                .WithPath($"/services/data/v59.0/sobjects/Account/{accountId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.TooManyRequests)
                .WithHeader("Content-Type", "application/json")
                .WithHeader("Retry-After", "60")
                .WithBodyAsJson(new[]
                {
                    new
                    {
                        message = "Request limit exceeded",
                        errorCode = "REQUEST_LIMIT_EXCEEDED"
                    }
                }));

        var createRequest = new CreateContractRequest(
            Title: "Test Contract",
            Description: "Test",
            Value: 10000m,
            ContractType: "Standard",
            SalesforceAccountId: accountId,
            SalesforceContactId: "003XX000004TmiD");

        // Act
        var response = await _client.PostAsJsonAsync("/api/contracts", createRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadGateway,
            HttpStatusCode.TooManyRequests,
            HttpStatusCode.InternalServerError);
    }

    /// <summary>
    /// 400 Bad Request - Invalid request to Salesforce
    /// </summary>
    [Fact]
    public async Task ValidateContract_WhenSalesforceReturnsBadRequest_HandlesError()
    {
        // Arrange
        const string accountId = "001XX000003DGb2";
        const string contactId = "003XX000004TmiD";

        SetupSuccessfulAccountAndContact(accountId, contactId);

        // Create a contract first
        var createRequest = new CreateContractRequest(
            Title: "Test Contract",
            Description: "Test",
            Value: 10000m,
            ContractType: "Standard",
            SalesforceAccountId: accountId,
            SalesforceContactId: contactId);

        var createResponse = await _client.PostAsJsonAsync("/api/contracts", createRequest);
        var contract = await createResponse.Content.ReadFromJsonAsync<ContractResponse>();

        // Now setup validation to return 400
        _wireMock.Server
            .Given(Request.Create()
                .WithPath("/services/data/v59.0/contract/validate")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.BadRequest)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new[]
                {
                    new
                    {
                        message = "Invalid contract type specified",
                        errorCode = "INVALID_FIELD",
                        fields = new[] { "ContractType" }
                    }
                }));

        // Act
        var validateResponse = await _client.PostAsync($"/api/contracts/{contract!.Id}/validate", null);

        // Assert
        validateResponse.StatusCode.Should().Be(HttpStatusCode.BadGateway);
    }

    private void SetupSuccessfulAccountAndContact(string accountId, string contactId)
    {
        _wireMock.Server
            .Given(Request.Create()
                .WithPath($"/services/data/v59.0/sobjects/Account/{accountId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { id = accountId, name = "Test Company", isActive = true }));

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
                    email = "test@test.com"
                }));
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }
}
