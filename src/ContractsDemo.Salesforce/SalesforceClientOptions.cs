namespace ContractsDemo.Salesforce;

public class SalesforceClientOptions
{
    public const string SectionName = "Salesforce";

    public required string BaseUrl { get; set; }
    public required string ClientId { get; set; }
    public required string ClientSecret { get; set; }
    public string ApiVersion { get; set; } = "v59.0";
    public int TimeoutSeconds { get; set; } = 30;
}
