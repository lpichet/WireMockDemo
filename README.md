# WireMock Demo - .NET 10 Aspire Application

A comprehensive demonstration of WireMock.Net features using a Contracts API with Salesforce integration.

## Project Structure

```
WireMockDemo/
├── src/
│   ├── ContractsDemo.AppHost/          # Aspire orchestrator with PostgreSQL
│   ├── ContractsDemo.ServiceDefaults/  # Shared Aspire service configuration
│   ├── ContractsDemo.Api/              # Contracts API (CRUD + validation + signing)
│   └── ContractsDemo.Salesforce/       # Reusable Salesforce client library
└── tests/
    └── ContractsDemo.Tests/            # WireMock integration tests
```

## Technologies

- **.NET 10** (Preview)
- **Aspire 9.3** - Cloud-native orchestration
- **EF Core 10** - Entity Framework with PostgreSQL
- **WireMock.Net** - HTTP mocking for integration tests
- **xUnit** - Testing framework
- **FluentAssertions** - Assertion library

## WireMock Features Demonstrated

### 1. Success Responses (`SuccessResponseTests.cs`)
```csharp
_wireMock.Server
    .Given(Request.Create()
        .WithPath("/api/account/123")
        .UsingGet())
    .RespondWith(Response.Create()
        .WithStatusCode(HttpStatusCode.OK)
        .WithBodyAsJson(new { id = "123", name = "Acme Corp" }));
```

### 2. Failure Scenarios (`FailureScenarioTests.cs`)
```csharp
// 404 Not Found
.RespondWith(Response.Create()
    .WithStatusCode(HttpStatusCode.NotFound))

// 500 Internal Server Error
.RespondWith(Response.Create()
    .WithStatusCode(HttpStatusCode.InternalServerError))

// 429 Rate Limited
.RespondWith(Response.Create()
    .WithStatusCode(HttpStatusCode.TooManyRequests)
    .WithHeader("Retry-After", "60"))
```

### 3. Fault Injection (`FaultInjectionTests.cs`)
```csharp
// Connection reset
.RespondWith(Response.Create()
    .WithFault(FaultType.EMPTY_RESPONSE))

// Malformed response
.RespondWith(Response.Create()
    .WithFault(FaultType.MALFORMED_RESPONSE_CHUNK))
```

### 4. Timeout Simulation (`TimeoutTests.cs`)
```csharp
// Fixed delay
.RespondWith(Response.Create()
    .WithDelay(TimeSpan.FromSeconds(10)))

// Random delay
.RespondWith(Response.Create()
    .WithRandomDelay(100, 1000))
```

### 5. Stateful Scenarios (`StatefulScenarioTests.cs`)
```csharp
// First request fails
_wireMock.Server
    .Given(Request.Create().WithPath("/api/validate").UsingPost())
    .InScenario("Retry Test")
    .WillSetStateTo("Failed Once")
    .RespondWith(Response.Create()
        .WithStatusCode(HttpStatusCode.ServiceUnavailable));

// Second request succeeds
_wireMock.Server
    .Given(Request.Create().WithPath("/api/validate").UsingPost())
    .InScenario("Retry Test")
    .WhenStateIs("Failed Once")
    .RespondWith(Response.Create()
        .WithStatusCode(HttpStatusCode.OK));
```

### 6. Request Pattern Matching (`RequestPatternMatchingTests.cs`)
```csharp
// JSON body matching
.Given(Request.Create()
    .WithPath("/api/contract/validate")
    .WithBody(new JsonPathMatcher("$[?(@.contractType == 'Enterprise')]")))

// Regex matching
.Given(Request.Create()
    .WithPath(new RegexMatcher(@"/api/account/[a-zA-Z0-9]{15,18}")))

// Header matching
.Given(Request.Create()
    .WithHeader("Authorization", new RegexMatcher("Bearer .*")))
```

## Running the Application

### Prerequisites
- .NET 10 SDK (Preview)
- Docker (for Aspire/PostgreSQL)

### Build
```bash
dotnet build
```

### Run Tests
```bash
dotnet test
```

### Run with Aspire
```bash
dotnet run --project src/ContractsDemo.AppHost
```

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/contracts` | List all contracts |
| GET | `/api/contracts/{id}` | Get contract by ID |
| POST | `/api/contracts` | Create a new contract |
| PUT | `/api/contracts/{id}` | Update a contract |
| DELETE | `/api/contracts/{id}` | Delete a contract |
| POST | `/api/contracts/{id}/validate` | Validate with Salesforce |
| POST | `/api/contracts/{id}/sign` | Sign a validated contract |

## Reusable Salesforce Client

The `ContractsDemo.Salesforce` project is a standalone library:

```csharp
// Register with configuration
services.AddSalesforceClient(configuration);

// Or with custom base URL (for testing with WireMock)
services.AddSalesforceClient("http://localhost:5000", options =>
{
    options.ApiVersion = "v59.0";
    options.TimeoutSeconds = 30;
});
```

## License

MIT
