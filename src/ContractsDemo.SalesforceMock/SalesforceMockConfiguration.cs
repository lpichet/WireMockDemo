using System.Net;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace ContractsDemo.SalesforceMock;

/// <summary>
/// Configures the WireMock server with realistic Salesforce API responses.
/// This can be used as a standalone development server or integrated into Aspire.
///
/// The mock supports multiple ways to control responses:
///
/// 1. HEADER-BASED CONTROL (X-Mock-Response header):
///    - "fail" or "error" → Returns 500 Internal Server Error
///    - "timeout" → Returns after 30 second delay
///    - "slow" → Returns after 5 second delay
///    - "not-found" → Returns 404 Not Found
///    - "unauthorized" → Returns 401 Unauthorized
///    - "rate-limit" → Returns 429 Too Many Requests
///    - "unavailable" → Returns 503 Service Unavailable
///
/// 2. ID-BASED CONTROL (include in Account/Contact ID):
///    - IDs containing "FAIL" → Returns 500 error
///    - IDs containing "NOTFOUND" → Returns 404
///    - IDs containing "TIMEOUT" → Returns after 30s delay
///    - IDs containing "SLOW" → Returns after 5s delay
///    - IDs containing "UNAUTH" → Returns 401
///
/// 3. QUERY PARAMETER CONTROL (?simulate=value):
///    - ?simulate=error → Returns 500 error
///    - ?simulate=timeout → Returns after 30s delay
///    - ?simulate=slow → Returns after 5s delay
///    - ?simulate=not-found → Returns 404
///
/// 4. CONTRACT VALUE-BASED (in request body - uses JsonPathMatcher):
///    - contractValue ≤ 100,000 → Auto-approved
///    - contractValue 100,001-500,000 → Pending manager approval
///    - contractValue > 500,000 → Rejected (exceeds credit limit)
///
/// 5. CONFIGURABLE RESPONSE DATA (via custom headers):
///    For Account endpoint, use these headers to customize response:
///    - X-Mock-Account-Name: Override the account name
///    - X-Mock-Account-Industry: Override the industry
///    - X-Mock-Account-Revenue: Override annual revenue
///    - X-Mock-Account-Employees: Override employee count
///
/// 6. DYNAMIC RESPONSE DATA (uses Handlebars transformer):
///    - Response body can include request data: {{request.pathSegments.[5]}}
///    - Parse JSON from request body: {{JsonPath.SelectToken request.body '$.field'}}
///    - Generate random data: {{Random Type="Guid"}}
///    - Include timestamps: {{DateTime.Now 'o'}}
///    - Conditional logic: {{#if value}}...{{else}}...{{/if}}
/// </summary>
public static class SalesforceMockConfiguration
{
    private static readonly TimeSpan TimeoutDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SlowDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Configures all mock endpoints on the WireMock server.
    /// </summary>
    public static void Configure(WireMockServer server)
    {
        // Configure response control mechanisms (highest priority)
        ConfigureHeaderBasedControl(server);
        ConfigureQueryParamControl(server);
        ConfigureIdBasedControl(server);

        // Configure standard endpoints
        ConfigureAccountEndpoints(server);
        ConfigureContactEndpoints(server);
        ConfigureContractValidationEndpoints(server);
        ConfigureContractNotificationEndpoints(server);
        ConfigureAdminEndpoints(server);
    }

    /// <summary>
    /// Configures header-based response control.
    /// Use X-Mock-Response header to control behavior.
    /// </summary>
    private static void ConfigureHeaderBasedControl(WireMockServer server)
    {
        // X-Mock-Response: fail or error → 500
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/.*"))
                .WithHeader("X-Mock-Response", new RegexMatcher("^(fail|error)$", true)))
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.InternalServerError)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new[]
                {
                    new { message = "Simulated server error via X-Mock-Response header", errorCode = "MOCK_ERROR" }
                }));

        // X-Mock-Response: timeout → 30s delay
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/.*"))
                .WithHeader("X-Mock-Response", new ExactMatcher(true, "timeout")))
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithDelay(TimeoutDelay)
                .WithBodyAsJson(new { message = "Response after timeout delay" }));

        // X-Mock-Response: slow → 5s delay
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/.*"))
                .WithHeader("X-Mock-Response", new ExactMatcher(true, "slow")))
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithDelay(SlowDelay)
                .WithBodyAsJson(new { message = "Response after slow delay" }));

        // X-Mock-Response: not-found → 404
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/.*"))
                .WithHeader("X-Mock-Response", new ExactMatcher(true, "not-found")))
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.NotFound)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new[]
                {
                    new { message = "Simulated not found via X-Mock-Response header", errorCode = "NOT_FOUND" }
                }));

        // X-Mock-Response: unauthorized → 401
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/.*"))
                .WithHeader("X-Mock-Response", new ExactMatcher(true, "unauthorized")))
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.Unauthorized)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new[]
                {
                    new { message = "Session expired or invalid", errorCode = "INVALID_SESSION_ID" }
                }));

        // X-Mock-Response: rate-limit → 429
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/.*"))
                .WithHeader("X-Mock-Response", new ExactMatcher(true, "rate-limit")))
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.TooManyRequests)
                .WithHeader("Content-Type", "application/json")
                .WithHeader("Retry-After", "60")
                .WithBodyAsJson(new[]
                {
                    new { message = "Request limit exceeded", errorCode = "REQUEST_LIMIT_EXCEEDED" }
                }));

        // X-Mock-Response: unavailable → 503
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/.*"))
                .WithHeader("X-Mock-Response", new ExactMatcher(true, "unavailable")))
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.ServiceUnavailable)
                .WithHeader("Content-Type", "application/json")
                .WithHeader("Retry-After", "30")
                .WithBodyAsJson(new[]
                {
                    new { message = "Service temporarily unavailable", errorCode = "SERVICE_UNAVAILABLE" }
                }));
    }

    /// <summary>
    /// Configures query parameter-based response control.
    /// Use ?simulate=value to control behavior.
    /// </summary>
    private static void ConfigureQueryParamControl(WireMockServer server)
    {
        // ?simulate=error → 500
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/.*"))
                .WithParam("simulate", "error"))
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.InternalServerError)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new[]
                {
                    new { message = "Simulated error via query parameter", errorCode = "MOCK_ERROR" }
                }));

        // ?simulate=timeout → 30s delay
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/.*"))
                .WithParam("simulate", "timeout"))
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithDelay(TimeoutDelay)
                .WithBodyAsJson(new { message = "Response after timeout simulation" }));

        // ?simulate=slow → 5s delay
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/.*"))
                .WithParam("simulate", "slow"))
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithDelay(SlowDelay)
                .WithBodyAsJson(new { message = "Response after slow simulation" }));

        // ?simulate=not-found → 404
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/.*"))
                .WithParam("simulate", "not-found"))
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.NotFound)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new[]
                {
                    new { message = "Simulated not found via query parameter", errorCode = "NOT_FOUND" }
                }));
    }

    /// <summary>
    /// Configures ID-based response control for Account and Contact endpoints.
    /// Include keywords in the ID to trigger specific behaviors.
    /// </summary>
    private static void ConfigureIdBasedControl(WireMockServer server)
    {
        // Account IDs containing "FAIL" → 500
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/v\d+\.\d+/sobjects/Account/.*FAIL.*"))
                .UsingGet())
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.InternalServerError)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new[]
                {
                    new { message = "Simulated failure for FAIL account ID", errorCode = "MOCK_ERROR" }
                }));

        // Account IDs containing "TIMEOUT" → 30s delay
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/v\d+\.\d+/sobjects/Account/.*TIMEOUT.*"))
                .UsingGet())
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithDelay(TimeoutDelay)
                .WithBodyAsJson(new
                {
                    id = "TIMEOUT_ACCOUNT",
                    name = "Timeout Test Account",
                    message = "Response after timeout delay"
                }));

        // Account IDs containing "SLOW" → 5s delay
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/v\d+\.\d+/sobjects/Account/.*SLOW.*"))
                .UsingGet())
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithDelay(SlowDelay)
                .WithBodyAsJson(new
                {
                    id = "SLOW_ACCOUNT",
                    name = "Slow Response Account",
                    message = "Response after slow delay"
                }));

        // Account IDs containing "UNAUTH" → 401
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/v\d+\.\d+/sobjects/Account/.*UNAUTH.*"))
                .UsingGet())
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.Unauthorized)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new[]
                {
                    new { message = "Session expired or invalid", errorCode = "INVALID_SESSION_ID" }
                }));

        // Contact IDs containing "FAIL" → 500
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/v\d+\.\d+/sobjects/Contact/.*FAIL.*"))
                .UsingGet())
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.InternalServerError)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new[]
                {
                    new { message = "Simulated failure for FAIL contact ID", errorCode = "MOCK_ERROR" }
                }));

        // Contact IDs containing "TIMEOUT" → 30s delay
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/v\d+\.\d+/sobjects/Contact/.*TIMEOUT.*"))
                .UsingGet())
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithDelay(TimeoutDelay)
                .WithBodyAsJson(new
                {
                    id = "TIMEOUT_CONTACT",
                    firstName = "Timeout",
                    lastName = "Test",
                    message = "Response after timeout delay"
                }));
    }

    private static void ConfigureAccountEndpoints(WireMockServer server)
    {
        // GET Account with X-Mock-Account-* headers for custom response values
        // Allows overriding specific fields via headers:
        // - X-Mock-Account-Name: Custom account name
        // - X-Mock-Account-Industry: Custom industry
        // - X-Mock-Account-Revenue: Custom annual revenue
        // - X-Mock-Account-Employees: Custom employee count
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/v\d+\.\d+/sobjects/Account/001[a-zA-Z0-9]{12,15}"))
                .WithHeader("X-Mock-Account-Name", new RegexMatcher(".*"))
                .UsingGet())
            .AtPriority(1)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                    "id": "{{request.pathSegments.[5]}}",
                    "name": "{{request.headers.X-Mock-Account-Name}}",
                    "industry": "{{#if request.headers.X-Mock-Account-Industry}}{{request.headers.X-Mock-Account-Industry}}{{else}}Technology{{/if}}",
                    "type": "Customer - Direct",
                    "billingCity": "San Francisco",
                    "billingState": "CA",
                    "billingCountry": "USA",
                    "billingPostalCode": "94105",
                    "phone": "+1-555-123-4567",
                    "website": "https://acme.example.com",
                    "annualRevenue": {{#if request.headers.X-Mock-Account-Revenue}}{{request.headers.X-Mock-Account-Revenue}}{{else}}50000000{{/if}},
                    "numberOfEmployees": {{#if request.headers.X-Mock-Account-Employees}}{{request.headers.X-Mock-Account-Employees}}{{else}}500{{/if}},
                    "isActive": true,
                    "createdDate": "{{DateTime.Now 'o'}}",
                    "lastModifiedDate": "{{DateTime.Now 'o'}}"
                }
                """)
                .WithTransformer());

        // GET Account - Default success response with realistic data (uses transformer)
        // The response extracts the ID from the request path and injects it
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/v\d+\.\d+/sobjects/Account/001[a-zA-Z0-9]{12,15}"))
                .UsingGet())
            .AtPriority(5)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                    "id": "{{request.pathSegments.[5]}}",
                    "name": "Acme Corporation",
                    "industry": "Technology",
                    "type": "Customer - Direct",
                    "billingCity": "San Francisco",
                    "billingState": "CA",
                    "billingCountry": "USA",
                    "billingPostalCode": "94105",
                    "phone": "+1-555-123-4567",
                    "website": "https://acme.example.com",
                    "annualRevenue": 50000000,
                    "numberOfEmployees": 500,
                    "isActive": true,
                    "createdDate": "{{DateTime.Now 'o'}}",
                    "lastModifiedDate": "{{DateTime.Now 'o'}}"
                }
                """)
                .WithTransformer());

        // GET Account - Not found (for specific test ID pattern)
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/v\d+\.\d+/sobjects/Account/001NOTFOUND\d+"))
                .UsingGet())
            .AtPriority(1)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.NotFound)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new[]
                {
                    new { message = "The requested resource does not exist", errorCode = "NOT_FOUND" }
                }));
    }

    private static void ConfigureContactEndpoints(WireMockServer server)
    {
        // GET Contact - Success response
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/v\d+\.\d+/sobjects/Contact/003[a-zA-Z0-9]{12,15}"))
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    id = "{{request.pathSegments.[5]}}",
                    accountId = "001XX000003DGb2YAG",
                    firstName = "Jane",
                    lastName = "Smith",
                    email = "jane.smith@acme.example.com",
                    phone = "+1-555-987-6543",
                    title = "VP of Engineering",
                    department = "Engineering",
                    mailingCity = "San Francisco",
                    mailingState = "CA",
                    mailingCountry = "USA"
                })
                .WithTransformer());

        // GET Contacts by Account
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/v\d+\.\d+/sobjects/Account/[a-zA-Z0-9]+/Contacts"))
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    records = new[]
                    {
                        new
                        {
                            id = "003XX000004TmiDAAS",
                            firstName = "Jane",
                            lastName = "Smith",
                            email = "jane.smith@acme.example.com",
                            title = "VP of Engineering"
                        },
                        new
                        {
                            id = "003XX000004TmiEAAS",
                            firstName = "John",
                            lastName = "Doe",
                            email = "john.doe@acme.example.com",
                            title = "Director of Sales"
                        }
                    }
                }));
    }

    private static void ConfigureContractValidationEndpoints(WireMockServer server)
    {
        // Validation - Approve standard contracts under $100k
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/v\d+\.\d+/contract/validate"))
                .UsingPost()
                .WithBody(new JsonPathMatcher("$[?(@.contractValue <= 100000)]")))
            .AtPriority(1)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithDelay(TimeSpan.FromMilliseconds(200)) // Simulate processing time
                .WithBodyAsJson(new
                {
                    isValid = true,
                    validationMessage = "Contract approved - within standard credit limits",
                    approvalStatus = "Auto-Approved",
                    creditLimit = 100000m,
                    requiredApprovers = Array.Empty<string>()
                }));

        // Validation - Require approval for contracts $100k-$500k
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/v\d+\.\d+/contract/validate"))
                .UsingPost()
                .WithBody(new JsonPathMatcher("$[?(@.contractValue > 100000 && @.contractValue <= 500000)]")))
            .AtPriority(2)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithDelay(TimeSpan.FromMilliseconds(500))
                .WithBodyAsJson(new
                {
                    isValid = true,
                    validationMessage = "Contract approved - requires manager approval before signing",
                    approvalStatus = "Pending Manager Approval",
                    creditLimit = 500000m,
                    requiredApprovers = new[] { "manager@company.com" }
                }));

        // Validation - Reject contracts over $500k (exceeds credit limit)
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/v\d+\.\d+/contract/validate"))
                .UsingPost()
                .WithBody(new JsonPathMatcher("$[?(@.contractValue > 500000)]")))
            .AtPriority(3)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithDelay(TimeSpan.FromMilliseconds(300))
                .WithBodyAsJson(new
                {
                    isValid = false,
                    validationMessage = "Contract value exceeds account credit limit. Requires executive approval and credit review.",
                    approvalStatus = "Rejected - Credit Limit",
                    creditLimit = 500000m,
                    requiredApprovers = new[] { "cfo@company.com", "ceo@company.com", "legal@company.com" }
                }));

        // Validation - Default fallback
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/v\d+\.\d+/contract/validate"))
                .UsingPost())
            .AtPriority(10)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    isValid = true,
                    validationMessage = "Contract validated successfully",
                    approvalStatus = "Approved"
                }));
    }

    private static void ConfigureContractNotificationEndpoints(WireMockServer server)
    {
        // Contract signed notification - demonstrates echoing request body data in response
        // The response includes data from the original request using JsonPath
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/v\d+\.\d+/contract/notify"))
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithDelay(TimeSpan.FromMilliseconds(100))
                .WithBody("""
                {
                    "success": true,
                    "message": "Salesforce CRM updated successfully",
                    "opportunityId": "006XX000001abcDEF",
                    "notificationId": "{{Random Type=\"Guid\"}}",
                    "processedAt": "{{DateTime.Now 'o'}}",
                    "echoedData": {
                        "contractId": "{{JsonPath.SelectToken request.body '$.contractId'}}",
                        "accountId": "{{JsonPath.SelectToken request.body '$.accountId'}}",
                        "signedBy": "{{JsonPath.SelectToken request.body '$.signedBy'}}"
                    }
                }
                """)
                .WithTransformer());
    }

    private static void ConfigureAdminEndpoints(WireMockServer server)
    {
        // Health check endpoint
        server
            .Given(Request.Create()
                .WithPath("/health")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    status = "healthy",
                    service = "Salesforce Mock Server",
                    timestamp = DateTime.UtcNow
                })
                .WithTransformer());

        // Admin endpoint to trigger error simulation
        server
            .Given(Request.Create()
                .WithPath("/__admin/simulate-error")
                .WithParam("type", "timeout")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithBodyAsJson(new { message = "Timeout simulation enabled" }));

        // List all mappings (for debugging)
        server
            .Given(Request.Create()
                .WithPath("/__admin/mappings")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{{JsonPath.SelectToken request.body '$'}}")
                .WithTransformer());
    }

    /// <summary>
    /// Adds a scenario-based response for testing retry logic.
    /// First 2 calls fail, third succeeds.
    /// </summary>
    public static void ConfigureRetryScenario(WireMockServer server, string scenarioName = "RetryScenario")
    {
        // First call - 503
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/v\d+\.\d+/contract/validate"))
                .UsingPost())
            .InScenario(scenarioName)
            .WillSetStateTo("FirstFailure")
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.ServiceUnavailable)
                .WithHeader("Retry-After", "1")
                .WithBodyAsJson(new { error = "Service temporarily unavailable" }));

        // Second call - 503
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/v\d+\.\d+/contract/validate"))
                .UsingPost())
            .InScenario(scenarioName)
            .WhenStateIs("FirstFailure")
            .WillSetStateTo("SecondFailure")
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.ServiceUnavailable)
                .WithHeader("Retry-After", "1")
                .WithBodyAsJson(new { error = "Still unavailable, please retry" }));

        // Third call - Success
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/v\d+\.\d+/contract/validate"))
                .UsingPost())
            .InScenario(scenarioName)
            .WhenStateIs("SecondFailure")
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    isValid = true,
                    validationMessage = "Contract validated after retry",
                    approvalStatus = "Approved"
                }));
    }

    /// <summary>
    /// Configures a slow response scenario for timeout testing.
    /// </summary>
    public static void ConfigureSlowResponseScenario(WireMockServer server, TimeSpan delay)
    {
        server
            .Given(Request.Create()
                .WithPath(new RegexMatcher(@"/services/data/v\d+\.\d+/contract/validate"))
                .WithHeader("X-Simulate-Slow", "true")
                .UsingPost())
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithDelay(delay)
                .WithBodyAsJson(new
                {
                    isValid = true,
                    validationMessage = "Slow response completed",
                    approvalStatus = "Approved"
                }));
    }
}
