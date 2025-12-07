using ContractsDemo.Api.Data;
using ContractsDemo.Salesforce;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContractsDemo.Tests.Fixtures;

/// <summary>
/// Custom WebApplicationFactory that configures the API to use WireMock
/// for Salesforce calls and InMemory database for testing.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _wireMockBaseUrl;

    public TestWebApplicationFactory(string wireMockBaseUrl)
    {
        _wireMockBaseUrl = wireMockBaseUrl;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Add configuration that provides a fake connection string
        // This prevents Aspire from throwing before we can replace the DbContext
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:contractsdb"] = "Host=localhost;Database=test;Username=test;Password=test"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove all DbContext related registrations
            var descriptorsToRemove = services
                .Where(d => d.ServiceType.FullName?.Contains("ContractsDbContext") == true ||
                            d.ImplementationType?.FullName?.Contains("ContractsDbContext") == true)
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            // Also remove by specific types
            services.RemoveAll<DbContextOptions<ContractsDbContext>>();

            // Add InMemory database
            services.AddDbContext<ContractsDbContext>(options =>
            {
                options.UseInMemoryDatabase($"ContractsTestDb_{Guid.NewGuid()}");
            });

            // Remove existing Salesforce client configuration
            services.RemoveAll<ISalesforceClient>();
            services.RemoveAll<SalesforceClient>();

            // Configure Salesforce client to use WireMock
            services.AddSalesforceClient(_wireMockBaseUrl, options =>
            {
                options.ApiVersion = "v59.0";
                options.TimeoutSeconds = 5; // Shorter timeout for tests
            });
        });
    }
}
