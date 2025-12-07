using System.Net;
using System.Net.Http.Json;
using ContractsDemo.Api.Models;
using ContractsDemo.Tests.Fixtures;
using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Settings;
using WireMock.Types;

namespace ContractsDemo.Tests.WireMock;

/// <summary>
/// Demonstrates WireMock FAULT INJECTION (.WithFault(...))
///
/// These tests show how to simulate network-level failures such as:
/// - Connection resets
/// - Malformed responses
/// - Empty responses
/// - Connection drops
///
/// This is crucial for testing resilience and circuit breaker patterns.
/// </summary>
[Collection("WireMock")]
public class FaultInjectionTests : IDisposable
{
    private readonly WireMockFixture _wireMock;
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FaultInjectionTests(WireMockFixture wireMock)
    {
        _wireMock = wireMock;
        _wireMock.Reset();
        _factory = new TestWebApplicationFactory(_wireMock.BaseUrl);
        _client = _factory.CreateClient();
    }

    /// <summary>
    /// Simulates a connection reset - the server abruptly closes the connection
    /// </summary>
    [Fact]
    public async Task CreateContract_WhenConnectionReset_HandlesGracefully()
    {
        // Arrange
        const string accountId = "001XX000003DGb2";

        _wireMock.Server
            .Given(Request.Create()
                .WithPath($"/services/data/v59.0/sobjects/Account/{accountId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithFault(FaultType.EMPTY_RESPONSE));

        var createRequest = new CreateContractRequest(
            Title: "Test Contract",
            Description: "Test",
            Value: 10000m,
            ContractType: "Standard",
            SalesforceAccountId: accountId,
            SalesforceContactId: "003XX000004TmiD");

        // Act
        var response = await _client.PostAsJsonAsync("/api/contracts", createRequest);

        // Assert - Should not crash, should return an error response
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadGateway,
            HttpStatusCode.InternalServerError);
    }

    /// <summary>
    /// Simulates malformed/invalid response data
    /// </summary>
    [Fact]
    public async Task CreateContract_WhenMalformedResponse_HandlesGracefully()
    {
        // Arrange
        const string accountId = "001XX000003DGb2";

        _wireMock.Server
            .Given(Request.Create()
                .WithPath($"/services/data/v59.0/sobjects/Account/{accountId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithFault(FaultType.MALFORMED_RESPONSE_CHUNK));

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
    /// Simulates random data garbage response
    /// </summary>
    [Fact]
    public async Task CreateContract_WhenRandomDataReturned_HandlesGracefully()
    {
        // Arrange
        const string accountId = "001XX000003DGb2";

        // Return invalid JSON
        _wireMock.Server
            .Given(Request.Create()
                .WithPath($"/services/data/v59.0/sobjects/Account/{accountId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{{{{not valid json at all!!!!"));

        var createRequest = new CreateContractRequest(
            Title: "Test Contract",
            Description: "Test",
            Value: 10000m,
            ContractType: "Standard",
            SalesforceAccountId: accountId,
            SalesforceContactId: "003XX000004TmiD");

        // Act
        var response = await _client.PostAsJsonAsync("/api/contracts", createRequest);

        // Assert - Should handle JSON parse error gracefully
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadGateway,
            HttpStatusCode.InternalServerError);
    }

    /// <summary>
    /// Simulates unexpected content type
    /// </summary>
    [Fact]
    public async Task CreateContract_WhenWrongContentType_HandlesGracefully()
    {
        // Arrange
        const string accountId = "001XX000003DGb2";

        _wireMock.Server
            .Given(Request.Create()
                .WithPath($"/services/data/v59.0/sobjects/Account/{accountId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "text/html")
                .WithBody("<html><body>Unexpected HTML response</body></html>"));

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
    /// Simulates empty JSON response
    /// </summary>
    [Fact]
    public async Task CreateContract_WhenEmptyJsonResponse_HandlesGracefully()
    {
        // Arrange
        const string accountId = "001XX000003DGb2";

        _wireMock.Server
            .Given(Request.Create()
                .WithPath($"/services/data/v59.0/sobjects/Account/{accountId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("null"));

        var createRequest = new CreateContractRequest(
            Title: "Test Contract",
            Description: "Test",
            Value: 10000m,
            ContractType: "Standard",
            SalesforceAccountId: accountId,
            SalesforceContactId: "003XX000004TmiD");

        // Act
        var response = await _client.PostAsJsonAsync("/api/contracts", createRequest);

        // Assert - Should handle null response
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Simulates partial/truncated JSON response
    /// </summary>
    [Fact]
    public async Task CreateContract_WhenTruncatedJson_HandlesGracefully()
    {
        // Arrange
        const string accountId = "001XX000003DGb2";

        _wireMock.Server
            .Given(Request.Create()
                .WithPath($"/services/data/v59.0/sobjects/Account/{accountId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"id\": \"001XX000003DGb2\", \"name\": \"Incomplete")); // Missing closing quotes and braces

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

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }
}
