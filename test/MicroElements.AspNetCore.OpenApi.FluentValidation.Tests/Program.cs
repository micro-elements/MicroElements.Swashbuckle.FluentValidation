// Minimal Program.cs for WebApplicationFactory<TestMarker> to discover the entry point.

using FluentValidation;
using MicroElements.AspNetCore.OpenApi.FluentValidation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddValidatorsFromAssemblyContaining<TestMarker>();
builder.Services.AddFluentValidationRulesToOpenApi();
builder.Services.AddOpenApi(options =>
{
    options.AddFluentValidationRules();
});

var app = builder.Build();

app.MapOpenApi();
app.MapPost("/api/customers", (TestCustomer customer) => Results.Ok(customer));
app.MapPost("/api/orders", (TestOrder order) => Results.Ok(order));

app.Run();
