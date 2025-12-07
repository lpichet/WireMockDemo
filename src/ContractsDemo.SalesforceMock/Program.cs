using ContractsDemo.SalesforceMock;
using WireMock.Server;
using WireMock.Settings;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults (OpenTelemetry, health checks, etc.)
builder.AddServiceDefaults();

// Get configuration
var port = builder.Configuration.GetValue<int?>("WireMock:Port") ?? 5100;
var enableAdminInterface = builder.Configuration.GetValue<bool>("WireMock:EnableAdmin", true);

// Create WireMock server with settings
var settings = new WireMockServerSettings
{
    Port = port,
    StartAdminInterface = enableAdminInterface,
    ReadStaticMappings = true,
    WatchStaticMappings = true,
    WatchStaticMappingsInSubdirectories = true
};

var wireMockServer = WireMockServer.Start(settings);

// Configure all mock endpoints
SalesforceMockConfiguration.Configure(wireMockServer);

// Log startup info
Console.WriteLine($$"""

    ╔═══════════════════════════════════════════════════════════════════════════════╗
    ║                    Salesforce Mock Server (WireMock.Net)                      ║
    ╠═══════════════════════════════════════════════════════════════════════════════╣
    ║  Base URL:     http://localhost:{{port}}
    ║  Admin UI:     http://localhost:{{port}}/__admin/mappings
    ║  Health:       http://localhost:{{port}}/health
    ╠═══════════════════════════════════════════════════════════════════════════════╣
    ║  ENDPOINTS:                                                                   ║
    ║  - GET  /services/data/v59.0/sobjects/Account/{id}                            ║
    ║  - GET  /services/data/v59.0/sobjects/Contact/{id}                            ║
    ║  - POST /services/data/v59.0/contract/validate                                ║
    ║  - POST /services/data/v59.0/contract/notify                                  ║
    ╠═══════════════════════════════════════════════════════════════════════════════╣
    ║  RESPONSE CONTROL:                                                            ║
    ║                                                                               ║
    ║  1. HEADER: X-Mock-Response                                                   ║
    ║     fail, error     → 500 Internal Server Error                               ║
    ║     timeout         → 30s delay                                               ║
    ║     slow            → 5s delay                                                ║
    ║     not-found       → 404 Not Found                                           ║
    ║     unauthorized    → 401 Unauthorized                                        ║
    ║     rate-limit      → 429 Too Many Requests                                   ║
    ║     unavailable     → 503 Service Unavailable                                 ║
    ║                                                                               ║
    ║  2. QUERY PARAM: ?simulate=                                                   ║
    ║     error, timeout, slow, not-found                                           ║
    ║                                                                               ║
    ║  3. ID PATTERNS (in Account/Contact ID):                                      ║
    ║     ...FAIL...      → 500 error                                               ║
    ║     ...TIMEOUT...   → 30s delay                                               ║
    ║     ...SLOW...      → 5s delay                                                ║
    ║     ...NOTFOUND...  → 404 error                                               ║
    ║     ...UNAUTH...    → 401 error                                               ║
    ║                                                                               ║
    ║  4. CONTRACT VALUE (in request body):                                         ║
    ║     <= 100,000      → Auto-approved                                           ║
    ║     100,001-500,000 → Pending manager approval                                ║
    ║     > 500,000       → Rejected (credit limit)                                 ║
    ║                                                                               ║
    ║  5. RUNTIME CONFIG: POST /__admin/config/account                              ║
    ║     Configure custom account responses at runtime                             ║
    ╚═══════════════════════════════════════════════════════════════════════════════╝

    Press Ctrl+C to stop the server.
    """);

// Build and run the ASP.NET Core app for health checks
var app = builder.Build();
app.MapDefaultEndpoints();

// Add a proxy endpoint that forwards to WireMock for Aspire integration
app.MapGet("/", () => Results.Redirect($"http://localhost:{port}/__admin/mappings"));

// Keep the application running
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    await app.RunAsync(cts.Token);
}
finally
{
    wireMockServer.Stop();
    wireMockServer.Dispose();
}

