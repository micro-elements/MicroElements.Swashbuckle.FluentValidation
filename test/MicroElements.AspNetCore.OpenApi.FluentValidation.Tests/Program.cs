// Minimal Program.cs for WebApplicationFactory<TestMarker> to discover the entry point.

using System.Globalization;
using FluentValidation;
using MicroElements.AspNetCore.OpenApi.FluentValidation;

// Document generation must be locale-independent: the net9/OpenApi 1.x writer formats fractional
// decimal bounds with the host culture (e.g. "5,1" under ru_RU), corrupting the JSON document.
System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddValidatorsFromAssemblyContaining<TestMarker>();

// Issue #236 binary smoke: the control factory disables our rules to compare framework-generated
// schemas with and without the transformers registered.
var enableValidationRules = builder.Configuration.GetValue("FvRules:Enabled", true);
if (enableValidationRules)
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
    if (enableValidationRules)
        options.AddFluentValidationRules();
});

// Issue #232: the multipart controller fixture lives in its own document. Components are per-document, and
// a second IFormFile-bearing endpoint in "v1" would promote IFormFile to a $ref component before the schema
// transformer reaches UploadImageRequest.file — a pre-existing net10 limitation of the Issue #216 description.
builder.Services.AddOpenApi("v2", options =>
{
    if (enableValidationRules)
        options.AddFluentValidationRules();
});

#if NET11_0_OR_GREATER
// Issue #236: explicit OpenAPI 3.1 document for runtime doc-version coverage; "v1" stays at the net11 default (3.2).
builder.Services.AddOpenApi("v31", options =>
{
    options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_1;
    if (enableValidationRules)
        options.AddFluentValidationRules();
});
#endif

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

app.MapPost("/api/child-rules-nullability", (TestChildRulesNullability model) => Results.Ok(model));

// Issue #236 regression endpoints.
app.MapPost("/api/shared-a", (TestSharedDto dto) => Results.Ok(dto));
app.MapPost("/api/shared-b", (TestSharedDto dto) => Results.Ok(dto));
app.MapPost("/api/ruleforeach", (TestRuleForEachModel model) => Results.Ok(model));
app.MapPost("/api/decimal-bounds", (TestDecimalBoundsModel model) => Results.Ok(model));

#if NET11_0_OR_GREATER
// Issue #236: framework-generated binary response schema smoke (net11-only). ASP.NET Core 11 maps
// FileContentResult to a component schema { type: string, format: binary } when the endpoint declares
// the response type via Produces<FileContentResult> (ASP.NET Core 11 release notes). Our transformers
// must not modify the framework-generated schema.
app.MapGet("/api/binary", () => TypedResults.File(new byte[] { 1, 2, 3 }, "application/octet-stream"))
    .Produces<Microsoft.AspNetCore.Mvc.FileContentResult>(contentType: "application/octet-stream");
#endif

app.Run();
