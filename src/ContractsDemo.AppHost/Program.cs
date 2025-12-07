var builder = DistributedApplication.CreateBuilder(args);

// Add PostgreSQL database
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin();

var contractsDb = postgres.AddDatabase("contractsdb");

// Add the Contracts API
var contractsApi = builder.AddProject("contracts-api", "../ContractsDemo.Api/ContractsDemo.Api.csproj")
    .WithReference(contractsDb)
    .WaitFor(contractsDb);

builder.Build().Run();
