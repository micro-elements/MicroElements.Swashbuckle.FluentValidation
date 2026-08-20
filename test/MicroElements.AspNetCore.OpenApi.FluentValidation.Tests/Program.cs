// Minimal Program.cs for WebApplicationFactory<TestMarker> to discover the entry point.

using FluentValidation;
using MicroElements.AspNetCore.OpenApi.FluentValidation;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddValidatorsFromAssemblyContaining<TestMarker>();
builder.Services.AddFluentValidationRulesToOpenApi();
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // Issue #230 follow-up: lets TraceHeaderController bind TestTraceHeaders properties
        // from headers instead of inferring the whole parameter as FromBody.
        // NOTE: this is a HOST-WIDE option — every controller in this test host must use
        // explicit binding-source attributes ([FromQuery]/[FromHeader]/...) on complex parameters.
        options.SuppressInferBindingSourcesForParameters = true;
    });
builder.Services.AddOpenApi(options =>
{
    options.AddFluentValidationRules();
});

// Issue #232: the multipart controller fixture lives in its own document. Components are per-document, and
// a second IFormFile-bearing endpoint in "v1" would promote IFormFile to a $ref component before the schema
// transformer reaches UploadImageRequest.file — a pre-existing net10 limitation of the Issue #216 description.
builder.Services.AddOpenApi("v2", options =>
{
    options.AddFluentValidationRules();
});

var app = builder.Build();

app.MapOpenApi();
app.MapControllers();
app.MapPost("/api/customers", (TestCustomer customer) => Results.Ok(customer));
app.MapPost("/api/orders", (TestOrder order) => Results.Ok(order));
app.MapPost("/api/biginteger", (TestBigIntegerModel model) => Results.Ok(model));
app.MapGet("/api/search", ([AsParameters] TestQueryParameters query) => Results.Ok(query));
app.MapGet("/api/filter", ([AsParameters] TestFilterParams filter) => Results.Ok(filter));
app.MapPost("/api/request", (TestRequestWithNested dto) => Results.Ok(dto));
app.MapPost("/api/collections", (TestCollectionModel model) => Results.Ok(model));
app.MapPost("/api/password", (TestPasswordModel model) => Results.Ok(model));
app.MapPost("/api/upload", ([Microsoft.AspNetCore.Mvc.FromForm] UploadImageRequest request) => Results.Ok()).DisableAntiforgery();

app.Run();
