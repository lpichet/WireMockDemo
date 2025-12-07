using System.Net;
using System.Net.Http.Json;
using ContractsDemo.Api.Models;
using ContractsDemo.Tests.Fixtures;
using FluentAssertions;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace ContractsDemo.Tests.WireMock;

/// <summary>
/// Demonstrates WireMock REQUEST PATTERN MATCHING
///
/// These tests show how to match specific request patterns:
/// - Body content matching (JSON, exact, regex)
/// - Header matching
/// - Query parameter matching
/// - Path pattern matching
///
/// This is essential for creating precise mocks that respond
/// differently based on request characteristics.
/// </summary>
[Collection("WireMock")]
public class RequestPatternMatchingTests : IDisposable
{
    private readonly WireMockFixture _wireMock;
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RequestPatternMatchingTests(WireMockFixture wireMock)
    {
        _wireMock = wireMock;
        _wireMock.Reset();
        _factory = new TestWebApplicationFactory(_wireMock.BaseUrl);
        _client = _factory.CreateClient();
    }

    /// <summary>
    /// Match requests based on JSON body content using JSONPath
    /// </summary>
    [Fact]
    public async Task ValidateContract_MatchesByContractType_ReturnsAppropriateResponse()
    {
        // Arrange
        const string accountId = "001XX000003JSON";
        const string contactId = "003XX000004JSON";

        SetupSuccessfulAccountAndContact(accountId, contactId);

        // Match Enterprise contracts - require extra approval
        _wireMock.Server
            .Given(Request.Create()
                .WithPath("/services/data/v59.0/contract/validate")
                .UsingPost()
                .WithBody(new JsonPathMatcher("$[?(@.contractType == 'Enterprise')]")))
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    isValid = true,
                    validationMessage = "Enterprise contract requires executive approval",
                    approvalStatus = "Pending Executive Approval",
                    requiredApprovers = new[] { "cfo@company.com", "ceo@company.com" }
                }));

        // Match Standard contracts - auto-approve
        _wireMock.Server
            .Given(Request.Create()
                .WithPath("/services/data/v59.0/contract/validate")
                .UsingPost()
                .WithBody(new JsonPathMatcher("$[?(@.contractType == 'Standard')]")))
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    isValid = true,
                    validationMessage = "Standard contract auto-approved",
                    approvalStatus = "Approved"
                }));

        // Create an Enterprise contract
        var enterpriseRequest = new CreateContractRequest(
            Title: "Enterprise Deal",
            Description: "Big enterprise contract",
            Value: 500000m,
            ContractType: "Enterprise",
            SalesforceAccountId: accountId,
            SalesforceContactId: contactId);

        var createResponse = await _client.PostAsJsonAsync("/api/contracts", enterpriseRequest);
        var contract = await createResponse.Content.ReadFromJsonAsync<ContractResponse>();

        // Act
        var validateResponse = await _client.PostAsync($"/api/contracts/{contract!.Id}/validate", null);

        // Assert - Should get Enterprise-specific response
        validateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var validatedContract = await validateResponse.Content.ReadFromJsonAsync<ContractResponse>();
        validatedContract!.ValidationMessage.Should().Contain("executive approval");
    }

    /// <summary>
    /// Match requests based on contract value using JSON body matching
    /// </summary>
    [Fact]
    public async Task ValidateContract_MatchesByContractValue_AppliesCreditCheck()
    {
        // Arrange
        const string accountId = "001XX000003VALUE";
        const string contactId = "003XX000004VALUE";

        SetupSuccessfulAccountAndContact(accountId, contactId);

        // High value contracts (> 100000) - reject exceeds credit
        _wireMock.Server
            .Given(Request.Create()
                .WithPath("/services/data/v59.0/contract/validate")
                .UsingPost()
                .WithBody(new JsonPathMatcher("$[?(@.contractValue > 100000)]")))
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    isValid = false,
                    validationMessage = "Contract value exceeds account credit limit of $100,000",
                    creditLimit = 100000m
                }));

        // Normal value contracts - approve
        _wireMock.Server
            .Given(Request.Create()
                .WithPath("/services/data/v59.0/contract/validate")
                .UsingPost()
                .WithBody(new JsonPathMatcher("$[?(@.contractValue <= 100000)]")))
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    isValid = true,
                    validationMessage = "Contract within credit limit",
                    creditLimit = 100000m
                }));

        // Create a high-value contract
        var highValueRequest = new CreateContractRequest(
            Title: "Big Deal",
            Description: "Very expensive contract",
            Value: 250000m, // Over 100k
            ContractType: "Standard",
            SalesforceAccountId: accountId,
            SalesforceContactId: contactId);

        var createResponse = await _client.PostAsJsonAsync("/api/contracts", highValueRequest);
        var contract = await createResponse.Content.ReadFromJsonAsync<ContractResponse>();

        // Act
        var validateResponse = await _client.PostAsync($"/api/contracts/{contract!.Id}/validate", null);

        // Assert
        validateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var validatedContract = await validateResponse.Content.ReadFromJsonAsync<ContractResponse>();
        validatedContract!.IsValidated.Should().BeFalse();
        validatedContract.ValidationMessage.Should().Contain("exceeds");
    }

    /// <summary>
    /// Match requests with specific headers
    /// </summary>
    [Fact]
    public async Task RequestWithAuthHeader_MatchesCorrectly()
    {
        // Arrange
        const string accountId = "001XX000003HEADER";

        // Only respond if Authorization header is present
        _wireMock.Server
            .Given(Request.Create()
                .WithPath($"/services/data/v59.0/sobjects/Account/{accountId}")
                .UsingGet()
                .WithHeader("Authorization", new RegexMatcher("Bearer .*")))
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    id = accountId,
                    name = "Authenticated Account",
                    isActive = true
                }));

        // Without auth header - return 401
        _wireMock.Server
            .Given(Request.Create()
                .WithPath($"/services/data/v59.0/sobjects/Account/{accountId}")
                .UsingGet())
            .AtPriority(10) // Lower priority than the one with header
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.Unauthorized)
                .WithBodyAsJson(new { error = "Missing authorization" }));

        // Act - Direct request without our app (to test WireMock pattern)
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer test-token");
        var response = await httpClient.GetAsync($"{_wireMock.BaseUrl}/services/data/v59.0/sobjects/Account/{accountId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Match requests with query parameters
    /// </summary>
    [Fact]
    public async Task RequestWithQueryParams_MatchesCorrectly()
    {
        // Arrange
        const string accountId = "001XX000003QUERY";

        // Match with specific query parameter
        _wireMock.Server
            .Given(Request.Create()
                .WithPath($"/services/data/v59.0/sobjects/Account/{accountId}")
                .WithParam("fields", "Id,Name,Industry")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    id = accountId,
                    name = "Partial Fields Account",
                    industry = "Technology"
                }));

        // Act
        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(
            $"{_wireMock.BaseUrl}/services/data/v59.0/sobjects/Account/{accountId}?fields=Id,Name,Industry");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Partial Fields Account");
    }

    /// <summary>
    /// Match requests using path patterns with wildcards
    /// </summary>
    [Fact]
    public async Task PathPatternMatching_MatchesMultipleAccounts()
    {
        // Arrange - Match any account ID
        _wireMock.Server
            .Given(Request.Create()
                .WithPath(new WildcardMatcher("/services/data/v59.0/sobjects/Account/*"))
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    id = "WILDCARD_MATCHED",
                    name = "Any Account",
                    isActive = true
                }));

        // Act - Request with any account ID
        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(
            $"{_wireMock.BaseUrl}/services/data/v59.0/sobjects/Account/ANY_ID_HERE");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Match requests using regex patterns
    /// </summary>
    [Fact]
    public async Task RegexPatternMatching_MatchesSalesforceIdFormat()
    {
        // Arrange - Match valid Salesforce ID format (15 or 18 alphanumeric chars)
        _wireMock.Server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/v59\.0/sobjects/Account/[a-zA-Z0-9]{15,18}"))
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    id = "REGEX_MATCHED",
                    name = "Regex Matched Account",
                    isActive = true
                }));

        // Invalid ID format - no match, returns 404 or similar
        _wireMock.Server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/v59\.0/sobjects/Account/invalid"))
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.BadRequest)
                .WithBodyAsJson(new { error = "Invalid Salesforce ID format" }));

        // Act
        using var httpClient = new HttpClient();
        var validResponse = await httpClient.GetAsync(
            $"{_wireMock.BaseUrl}/services/data/v59.0/sobjects/Account/001XX000003DGb2Y");
        var invalidResponse = await httpClient.GetAsync(
            $"{_wireMock.BaseUrl}/services/data/v59.0/sobjects/Account/invalid");

        // Assert
        validResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        invalidResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Match requests with exact body content
    /// </summary>
    [Fact]
    public async Task ExactBodyMatching_MatchesSpecificPayload()
    {
        // Arrange
        const string exactJson = """{"accountId":"001XX","contactId":"003XX","contractValue":50000,"contractType":"Standard"}""";

        _wireMock.Server
            .Given(Request.Create()
                .WithPath("/services/data/v59.0/contract/validate")
                .UsingPost()
                .WithBody(new ExactMatcher(exactJson)))
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    isValid = true,
                    validationMessage = "Exact match found!"
                }));

        // Act
        using var httpClient = new HttpClient();
        var content = new StringContent(exactJson, System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(
            $"{_wireMock.BaseUrl}/services/data/v59.0/contract/validate",
            content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("Exact match");
    }

    /// <summary>
    /// Combine multiple matchers for complex conditions
    /// </summary>
    [Fact]
    public async Task CombinedMatchers_MatchComplexConditions()
    {
        // Arrange - Match specific path, header, and body pattern
        _wireMock.Server
            .Given(Request.Create()
                .WithPath("/services/data/v59.0/contract/validate")
                .UsingPost()
                .WithHeader("Content-Type", new WildcardMatcher("application/json*"))
                .WithBody(new JsonPartialMatcher(new { contractType = "Premium" })))
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    isValid = true,
                    validationMessage = "Premium contract validated with VIP treatment",
                    approvalStatus = "VIP Auto-Approved"
                }));

        // Act
        using var httpClient = new HttpClient();
        var payload = new
        {
            accountId = "001XX",
            contactId = "003XX",
            contractValue = 75000,
            contractType = "Premium"
        };
        var response = await httpClient.PostAsJsonAsync(
            $"{_wireMock.BaseUrl}/services/data/v59.0/contract/validate",
            payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("VIP");
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
