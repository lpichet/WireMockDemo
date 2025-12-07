using ContractsDemo.Api.Models;
using ContractsDemo.Api.Services;

namespace ContractsDemo.Api.Endpoints;

public static class ContractEndpoints
{
    public static IEndpointRouteBuilder MapContractEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/contracts")
            .WithTags("Contracts")
            .WithOpenApi();

        group.MapGet("/", GetContracts)
            .WithName("GetContracts")
            .WithSummary("Get all contracts");

        group.MapGet("/{id:guid}", GetContract)
            .WithName("GetContract")
            .WithSummary("Get a contract by ID");

        group.MapPost("/", CreateContract)
            .WithName("CreateContract")
            .WithSummary("Create a new contract");

        group.MapPut("/{id:guid}", UpdateContract)
            .WithName("UpdateContract")
            .WithSummary("Update an existing contract");

        group.MapDelete("/{id:guid}", DeleteContract)
            .WithName("DeleteContract")
            .WithSummary("Delete a contract");

        group.MapPost("/{id:guid}/validate", ValidateContract)
            .WithName("ValidateContract")
            .WithSummary("Validate a contract with Salesforce");

        group.MapPost("/{id:guid}/sign", SignContract)
            .WithName("SignContract")
            .WithSummary("Sign a validated contract");

        return app;
    }

    private static async Task<IResult> GetContracts(IContractService contractService, CancellationToken cancellationToken)
    {
        var contracts = await contractService.GetContractsAsync(cancellationToken);
        return Results.Ok(contracts);
    }

    private static async Task<IResult> GetContract(Guid id, IContractService contractService, CancellationToken cancellationToken)
    {
        var contract = await contractService.GetContractAsync(id, cancellationToken);
        return contract is null ? Results.NotFound() : Results.Ok(contract);
    }

    private static async Task<IResult> CreateContract(
        CreateContractRequest request,
        IContractService contractService,
        CancellationToken cancellationToken)
    {
        try
        {
            var contract = await contractService.CreateContractAsync(request, cancellationToken);
            return Results.Created($"/api/contracts/{contract.Id}", contract);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> UpdateContract(
        Guid id,
        UpdateContractRequest request,
        IContractService contractService,
        CancellationToken cancellationToken)
    {
        try
        {
            var contract = await contractService.UpdateContractAsync(id, request, cancellationToken);
            return contract is null ? Results.NotFound() : Results.Ok(contract);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> DeleteContract(
        Guid id,
        IContractService contractService,
        CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await contractService.DeleteContractAsync(id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> ValidateContract(
        Guid id,
        IContractService contractService,
        CancellationToken cancellationToken)
    {
        try
        {
            var contract = await contractService.ValidateContractAsync(id, cancellationToken);
            return contract is null ? Results.NotFound() : Results.Ok(contract);
        }
        catch (HttpRequestException ex)
        {
            return Results.Problem(
                detail: $"Failed to validate contract with Salesforce: {ex.Message}",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static async Task<IResult> SignContract(
        Guid id,
        SignContractRequest request,
        IContractService contractService,
        CancellationToken cancellationToken)
    {
        try
        {
            var contract = await contractService.SignContractAsync(id, request, cancellationToken);
            return contract is null ? Results.NotFound() : Results.Ok(contract);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            return Results.Problem(
                detail: $"Failed to notify Salesforce: {ex.Message}",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
