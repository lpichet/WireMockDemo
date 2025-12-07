var builder = DistributedApplication.CreateBuilder(args);

// Add PostgreSQL database
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin();

var contractsDb = postgres.AddDatabase("contractsdb");

// Add Salesforce Mock Server (WireMock)
// This provides a realistic Salesforce API mock for development
var salesforceMock = builder.AddProject("salesforce-mock", "../ContractsDemo.SalesforceMock/ContractsDemo.SalesforceMock.csproj")
    .WithHttpEndpoint(port: 5100, name: "wiremock");

// Add the Contracts API
var contractsApi = builder.AddProject("contracts-api", "../ContractsDemo.Api/ContractsDemo.Api.csproj")
    .WithReference(contractsDb)
    .WithReference(salesforceMock)
    .WaitFor(contractsDb)
    .WaitFor(salesforceMock)
    .WithEnvironment("Salesforce__BaseUrl", salesforceMock.GetEndpoint("wiremock"));

builder.Build().Run();
