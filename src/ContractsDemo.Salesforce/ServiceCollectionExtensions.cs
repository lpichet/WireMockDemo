using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContractsDemo.Salesforce;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Salesforce client to the service collection.
    /// This is a reusable registration that can be used by any project.
    /// </summary>
    public static IServiceCollection AddSalesforceClient(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IHttpClientBuilder>? configureHttpClient = null)
    {
        services.Configure<SalesforceClientOptions>(
            configuration.GetSection(SalesforceClientOptions.SectionName));

        var httpClientBuilder = services.AddHttpClient<ISalesforceClient, SalesforceClient>((sp, client) =>
        {
            var options = configuration
                .GetSection(SalesforceClientOptions.SectionName)
                .Get<SalesforceClientOptions>();

            if (options is not null)
            {
                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            }
        });

        configureHttpClient?.Invoke(httpClientBuilder);

        return services;
    }

    /// <summary>
    /// Adds the Salesforce client with a custom base URL.
    /// Useful for testing with WireMock or other mock servers.
    /// </summary>
    public static IServiceCollection AddSalesforceClient(
        this IServiceCollection services,
        string baseUrl,
        Action<SalesforceClientOptions>? configure = null)
    {
        var options = new SalesforceClientOptions
        {
            BaseUrl = baseUrl,
            ClientId = "test",
            ClientSecret = "test"
        };

        configure?.Invoke(options);

        services.Configure<SalesforceClientOptions>(opt =>
        {
            opt.BaseUrl = options.BaseUrl;
            opt.ClientId = options.ClientId;
            opt.ClientSecret = options.ClientSecret;
            opt.ApiVersion = options.ApiVersion;
            opt.TimeoutSeconds = options.TimeoutSeconds;
        });

        services.AddHttpClient<ISalesforceClient, SalesforceClient>((sp, client) =>
        {
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        return services;
    }
}
