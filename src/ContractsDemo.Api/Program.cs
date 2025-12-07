using ContractsDemo.Api.Data;
using ContractsDemo.Api.Endpoints;
using ContractsDemo.Api.Services;
using ContractsDemo.Salesforce;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components
builder.AddServiceDefaults();

// Add PostgreSQL with EF Core
builder.AddNpgsqlDbContext<ContractsDbContext>("contractsdb");

// Add Salesforce client
builder.Services.AddSalesforceClient(builder.Configuration);

// Add services
builder.Services.AddScoped<IContractService, ContractService>();

// Add OpenAPI/Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ContractsDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.MapDefaultEndpoints();
app.MapContractEndpoints();

app.Run();

// Make the implicit Program class public so test projects can access it
public partial class Program { }
