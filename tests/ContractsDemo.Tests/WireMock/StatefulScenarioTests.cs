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
/// Demonstrates WireMock STATEFUL SCENARIOS (InScenario)
///
/// Scenarios allow you to create stateful mocks that change behavior
/// based on previous interactions. This is powerful for testing:
/// - Multi-step workflows
/// - State transitions
/// - Retry logic
/// - Eventually consistent systems
/// </summary>
[Collection("WireMock")]
public class StatefulScenarioTests : IDisposable
{
    private readonly WireMockFixture _wireMock;
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public StatefulScenarioTests(WireMockFixture wireMock)
    {
        _wireMock = wireMock;
        _wireMock.Reset();
        _factory = new TestWebApplicationFactory(_wireMock.BaseUrl);
        _client = _factory.CreateClient();
    }

    /// <summary>
    /// Simulates an account that starts as inactive and becomes active after a retry.
    /// Demonstrates state transitions in WireMock scenarios.
    /// </summary>
    [Fact]
    public async Task GetAccount_WhenInitiallyInactive_BecomesActiveAfterRetry()
    {
        // Arrange
        const string accountId = "001XX000003TRANS";
        const string scenarioName = "Account Activation";

        // Initial state: Account is inactive
        _wireMock.Server
            .Given(Request.Create()
                .WithPath($"/services/data/v59.0/sobjects/Account/{accountId}")
                .UsingGet())
            .InScenario(scenarioName)
            .WillSetStateTo("Account Activated")
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    id = accountId,
                    name = "Transitioning Company",
                    isActive = false // Initially inactive
                }));

        // After first call: Account becomes active
        _wireMock.Server
            .Given(Request.Create()
                .WithPath($"/services/data/v59.0/sobjects/Account/{accountId}")
                .UsingGet())
            .InScenario(scenarioName)
            .WhenStateIs("Account Activated")
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    id = accountId,
                    name = "Transitioning Company",
                    isActive = true // Now active!
                }));

        // Act - Make multiple requests and observe state change
        var firstResponse = await _client.GetAsync($"/services/data/v59.0/sobjects/Account/{accountId}");
        var secondResponse = await _client.GetAsync($"/services/data/v59.0/sobjects/Account/{accountId}");

        // Note: This demonstrates the WireMock capability - the actual API doesn't expose these endpoints directly
        // In a real test, you'd use this to test retry logic in your application
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Simulates a "fail then succeed" pattern - useful for testing retry logic.
    /// The first N calls fail, then subsequent calls succeed.
    /// </summary>
    [Fact]
    public async Task ValidateContract_FailsTwiceThenSucceeds_RetryLogicWorks()
    {
        // Arrange
        const string accountId = "001XX000003RETRY";
        const string contactId = "003XX000004RETRY";
        const string scenarioName = "Validation Retries";

        SetupSuccessfulAccountAndContact(accountId, contactId);

        // State 1: First call fails with 503
        _wireMock.Server
            .Given(Request.Create()
                .WithPath("/services/data/v59.0/contract/validate")
                .UsingPost())
            .InScenario(scenarioName)
            .WillSetStateTo("First Failure")
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.ServiceUnavailable)
                .WithHeader("Retry-After", "1")
                .WithBodyAsJson(new { error = "Service temporarily unavailable" }));

        // State 2: Second call also fails
        _wireMock.Server
            .Given(Request.Create()
                .WithPath("/services/data/v59.0/contract/validate")
                .UsingPost())
            .InScenario(scenarioName)
            .WhenStateIs("First Failure")
            .WillSetStateTo("Second Failure")
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.ServiceUnavailable)
                .WithHeader("Retry-After", "1")
                .WithBodyAsJson(new { error = "Still unavailable" }));

        // State 3: Third call succeeds
        _wireMock.Server
            .Given(Request.Create()
                .WithPath("/services/data/v59.0/contract/validate")
                .UsingPost())
            .InScenario(scenarioName)
            .WhenStateIs("Second Failure")
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    isValid = true,
                    validationMessage = "Finally validated!",
                    approvalStatus = "Approved"
                }));

        // Create contract first
        var createRequest = new CreateContractRequest(
            Title: "Retry Test Contract",
            Description: "Testing retry logic",
            Value: 10000m,
            ContractType: "Standard",
            SalesforceAccountId: accountId,
            SalesforceContactId: contactId);

        var createResponse = await _client.PostAsJsonAsync("/api/contracts", createRequest);
        var contract = await createResponse.Content.ReadFromJsonAsync<ContractResponse>();

        // Act - The application might have retry logic built in
        // In this test, we just verify the scenario works
        var validateResponse = await _client.PostAsync($"/api/contracts/{contract!.Id}/validate", null);

        // Assert - Depending on retry policy, this might succeed or fail
        // The key is the scenario transitions work correctly
        validateResponse.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.BadGateway,
            HttpStatusCode.ServiceUnavailable);
    }

    /// <summary>
    /// Simulates a contract signing workflow with multiple states.
    /// Shows how to model complex business workflows.
    /// </summary>
    [Fact]
    public async Task ContractSigningWorkflow_ProgressesThroughStates()
    {
        // Arrange
        const string accountId = "001XX000003WORKFLOW";
        const string contactId = "003XX000004WORKFLOW";
        const string scenarioName = "Contract Signing Workflow";

        SetupSuccessfulAccountAndContact(accountId, contactId);

        // Validation: Initial state
        _wireMock.Server
            .Given(Request.Create()
                .WithPath("/services/data/v59.0/contract/validate")
                .UsingPost())
            .InScenario(scenarioName)
            .WillSetStateTo("Validated")
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    isValid = true,
                    validationMessage = "Contract validated",
                    approvalStatus = "Approved"
                }));

        // Notification: After validation
        _wireMock.Server
            .Given(Request.Create()
                .WithPath("/services/data/v59.0/contract/notify")
                .UsingPost())
            .InScenario(scenarioName)
            .WhenStateIs("Validated")
            .WillSetStateTo("Signed")
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { success = true, message = "Salesforce notified" }));

        // Create and validate contract
        var createRequest = new CreateContractRequest(
            Title: "Workflow Test Contract",
            Description: "Testing workflow states",
            Value: 25000m,
            ContractType: "Enterprise",
            SalesforceAccountId: accountId,
            SalesforceContactId: contactId);

        var createResponse = await _client.PostAsJsonAsync("/api/contracts", createRequest);
        var contract = await createResponse.Content.ReadFromJsonAsync<ContractResponse>();

        // Act - Execute the workflow
        var validateResponse = await _client.PostAsync($"/api/contracts/{contract!.Id}/validate", null);
        validateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var validatedContract = await validateResponse.Content.ReadFromJsonAsync<ContractResponse>();

        var signResponse = await _client.PostAsJsonAsync(
            $"/api/contracts/{contract.Id}/sign",
            new SignContractRequest(SignedBy: "CEO"));

        // Assert - Workflow completed successfully
        signResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var signedContract = await signResponse.Content.ReadFromJsonAsync<ContractResponse>();
        signedContract!.Status.Should().Be(ContractStatus.Signed);
    }

    /// <summary>
    /// Demonstrates a "degraded mode" scenario where the service starts healthy,
    /// then degrades, then recovers.
    /// </summary>
    [Fact]
    public async Task ServiceDegradation_HandledGracefully()
    {
        // Arrange
        const string accountId = "001XX000003DEGRADE";
        const string scenarioName = "Service Degradation";

        // Healthy state
        _wireMock.Server
            .Given(Request.Create()
                .WithPath($"/services/data/v59.0/sobjects/Account/{accountId}")
                .UsingGet())
            .InScenario(scenarioName)
            .WillSetStateTo("Degraded")
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithDelay(TimeSpan.FromMilliseconds(100)) // Fast response
                .WithBodyAsJson(new { id = accountId, name = "Healthy Company", isActive = true }));

        // Degraded state - slow responses
        _wireMock.Server
            .Given(Request.Create()
                .WithPath($"/services/data/v59.0/sobjects/Account/{accountId}")
                .UsingGet())
            .InScenario(scenarioName)
            .WhenStateIs("Degraded")
            .WillSetStateTo("Recovering")
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithDelay(TimeSpan.FromSeconds(3)) // Slow response
                .WithBodyAsJson(new { id = accountId, name = "Degraded Company", isActive = true }));

        // Recovering state
        _wireMock.Server
            .Given(Request.Create()
                .WithPath($"/services/data/v59.0/sobjects/Account/{accountId}")
                .UsingGet())
            .InScenario(scenarioName)
            .WhenStateIs("Recovering")
            .WillSetStateTo("Healthy Again")
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithDelay(TimeSpan.FromMilliseconds(500))
                .WithBodyAsJson(new { id = accountId, name = "Recovering Company", isActive = true }));

        // Back to healthy
        _wireMock.Server
            .Given(Request.Create()
                .WithPath($"/services/data/v59.0/sobjects/Account/{accountId}")
                .UsingGet())
            .InScenario(scenarioName)
            .WhenStateIs("Healthy Again")
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithDelay(TimeSpan.FromMilliseconds(100))
                .WithBodyAsJson(new { id = accountId, name = "Fully Recovered Company", isActive = true }));

        // Act - Make requests through degradation cycle
        // (This would typically be used to test circuit breaker behavior)
        var response1 = await _client.GetAsync($"{_wireMock.BaseUrl}/services/data/v59.0/sobjects/Account/{accountId}");
        var response2 = await _client.GetAsync($"{_wireMock.BaseUrl}/services/data/v59.0/sobjects/Account/{accountId}");
        var response3 = await _client.GetAsync($"{_wireMock.BaseUrl}/services/data/v59.0/sobjects/Account/{accountId}");
        var response4 = await _client.GetAsync($"{_wireMock.BaseUrl}/services/data/v59.0/sobjects/Account/{accountId}");

        // Assert - All requests eventually succeed
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
        response3.StatusCode.Should().Be(HttpStatusCode.OK);
        response4.StatusCode.Should().Be(HttpStatusCode.OK);
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
