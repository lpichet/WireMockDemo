using System.Net;
using System.Net.Http.Json;
using ContractsDemo.Api.Models;
using ContractsDemo.Tests.Fixtures;
using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace ContractsDemo.Tests.WireMock;

/// <summary>
/// Demonstrates WireMock TIMEOUT SIMULATION (.WithDelay(...))
///
/// These tests show how to simulate slow or hanging API responses.
/// Essential for testing timeout handling and ensuring your application
/// doesn't hang indefinitely waiting for external services.
/// </summary>
[Collection("WireMock")]
public class TimeoutTests : IDisposable
{
    private readonly WireMockFixture _wireMock;
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TimeoutTests(WireMockFixture wireMock)
    {
        _wireMock = wireMock;
        _wireMock.Reset();
        _factory = new TestWebApplicationFactory(_wireMock.BaseUrl);
        _client = _factory.CreateClient();
    }

    /// <summary>
    /// Simulates a slow response that exceeds timeout
    /// The test factory configures a 5-second timeout for Salesforce calls
    /// </summary>
    [Fact]
    public async Task CreateContract_WhenSalesforceTimesOut_HandlesGracefully()
    {
        // Arrange
        const string accountId = "001XX000003DGb2";

        // Configure a 10-second delay - exceeds the 5-second timeout
        _wireMock.Server
            .Given(Request.Create()
                .WithPath($"/services/data/v59.0/sobjects/Account/{accountId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithDelay(TimeSpan.FromSeconds(10)) // Longer than timeout
                .WithBodyAsJson(new { id = accountId, name = "Slow Company" }));

        var createRequest = new CreateContractRequest(
            Title: "Test Contract",
            Description: "Test",
            Value: 10000m,
            ContractType: "Standard",
            SalesforceAccountId: accountId,
            SalesforceContactId: "003XX000004TmiD");

        // Act
        var response = await _client.PostAsJsonAsync("/api/contracts", createRequest);

        // Assert - Should timeout and return error
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadGateway,
            HttpStatusCode.GatewayTimeout,
            HttpStatusCode.InternalServerError);
    }

    /// <summary>
    /// Simulates a slow but successful response (within timeout)
    /// </summary>
    [Fact]
    public async Task CreateContract_WhenSlowButWithinTimeout_Succeeds()
    {
        // Arrange
        const string accountId = "001XX000003DGb2";
        const string contactId = "003XX000004TmiD";

        // Configure a 2-second delay - within the 5-second timeout
        _wireMock.Server
            .Given(Request.Create()
                .WithPath($"/services/data/v59.0/sobjects/Account/{accountId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithDelay(TimeSpan.FromSeconds(2))
                .WithBodyAsJson(new
                {
                    id = accountId,
                    name = "Slow But Successful Company",
                    isActive = true
                }));

        _wireMock.Server
            .Given(Request.Create()
                .WithPath($"/services/data/v59.0/sobjects/Contact/{contactId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithDelay(TimeSpan.FromMilliseconds(500))
                .WithBodyAsJson(new
                {
                    id = contactId,
                    accountId = accountId,
                    firstName = "Slow",
                    lastName = "User",
                    email = "slow@test.com"
                }));

        var createRequest = new CreateContractRequest(
            Title: "Test Contract",
            Description: "Test",
            Value: 10000m,
            ContractType: "Standard",
            SalesforceAccountId: accountId,
            SalesforceContactId: contactId);

        // Act
        var response = await _client.PostAsJsonAsync("/api/contracts", createRequest);

        // Assert - Should succeed despite being slow
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>
    /// Simulates variable latency using random delay
    /// Useful for testing behavior under unpredictable network conditions
    /// </summary>
    [Fact]
    public async Task CreateContract_WithVariableLatency_HandlesCorrectly()
    {
        // Arrange
        const string accountId = "001XX000003DGb2";
        const string contactId = "003XX000004TmiD";

        // Configure random delay between 100ms and 1 second
        _wireMock.Server
            .Given(Request.Create()
                .WithPath($"/services/data/v59.0/sobjects/Account/{accountId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithRandomDelay(100, 1000) // Random delay 100ms-1000ms
                .WithBodyAsJson(new
                {
                    id = accountId,
                    name = "Variable Latency Company",
                    isActive = true
                }));

        _wireMock.Server
            .Given(Request.Create()
                .WithPath($"/services/data/v59.0/sobjects/Contact/{contactId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithRandomDelay(100, 500)
                .WithBodyAsJson(new
                {
                    id = contactId,
                    accountId = accountId,
                    firstName = "Variable",
                    lastName = "User",
                    email = "variable@test.com"
                }));

        var createRequest = new CreateContractRequest(
            Title: "Test Contract",
            Description: "Test",
            Value: 10000m,
            ContractType: "Standard",
            SalesforceAccountId: accountId,
            SalesforceContactId: contactId);

        // Act
        var response = await _client.PostAsJsonAsync("/api/contracts", createRequest);

        // Assert - Should still succeed
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>
    /// Tests timeout behavior during contract validation
    /// </summary>
    [Fact]
    public async Task ValidateContract_WhenValidationTimesOut_ReturnsBadGateway()
    {
        // Arrange
        const string accountId = "001XX000003DGb2";
        const string contactId = "003XX000004TmiD";

        // Setup successful account/contact lookup
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

        // Create contract first
        var createRequest = new CreateContractRequest(
            Title: "Test Contract",
            Description: "Test",
            Value: 10000m,
            ContractType: "Standard",
            SalesforceAccountId: accountId,
            SalesforceContactId: contactId);

        var createResponse = await _client.PostAsJsonAsync("/api/contracts", createRequest);
        var contract = await createResponse.Content.ReadFromJsonAsync<ContractResponse>();

        // Setup validation endpoint to timeout
        _wireMock.Server
            .Given(Request.Create()
                .WithPath("/services/data/v59.0/contract/validate")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithDelay(TimeSpan.FromSeconds(10)) // Timeout
                .WithBodyAsJson(new { isValid = true, validationMessage = "OK" }));

        // Act
        var validateResponse = await _client.PostAsync($"/api/contracts/{contract!.Id}/validate", null);

        // Assert
        validateResponse.StatusCode.Should().Be(HttpStatusCode.BadGateway);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }
}
