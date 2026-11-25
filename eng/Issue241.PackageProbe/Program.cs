using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using FluentValidation;
using MicroElements.OpenApi.FluentValidation.FileUpload;
using MicroElements.Swashbuckle.FluentValidation;
using MicroElements.Swashbuckle.FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
#if SWASHBUCKLE_11
using MediaType = Microsoft.OpenApi.IOpenApiMediaType;
#else
using MediaType = Microsoft.OpenApi.OpenApiMediaType;
#endif

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
builder.WebHost.UseTestServer();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen(options => options.OperationFilter<PrepareRequestBody>());
builder.Services.AddScoped<IValidator<JsonRequest>, JsonRequestValidator>();
builder.Services.AddScoped<IValidator<UploadRequest>, UploadRequestValidator>();
builder.Services.AddScoped<IValidator<FormRequest>, FormRequestValidator>();
var useDocumentFilter = Environment.GetEnvironmentVariable("ISSUE241_DOCUMENT_FILTER") == "1";
builder.Services.AddFluentValidationRulesToSwagger(configureRegistration: options => options.UseDocumentFilter = useDocumentFilter);

await using var app = builder.Build();
app.UseSwagger();
app.MapControllers();
await app.StartAsync();

Console.WriteLine(RuntimeInformation.FrameworkDescription);
Console.WriteLine(useDocumentFilter ? "Document filter" : "Operation filter");
foreach (var assembly in new[]
{
    typeof(WebApplication).Assembly, typeof(SwaggerGenerator).Assembly,
    typeof(OpenApiRequestBody).Assembly, typeof(FluentValidationOperationFilter).Assembly,
})
{
    Console.WriteLine($"{assembly.GetName().Name} {assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion}");
}

using var client = app.GetTestClient();
using var response = await client.GetAsync("/swagger/v1/swagger.json");
var body = await response.Content.ReadAsStringAsync();
if (args.Length == 1)
    await File.WriteAllTextAsync(args[0], body);
Console.WriteLine($"Swagger HTTP {(int)response.StatusCode}");
response.EnsureSuccessStatusCode();

using var document = JsonDocument.Parse(body);
var root = document.RootElement;
JsonElement Operation(string path, string method = "post")
    => root.GetProperty("paths").GetProperty(path).GetProperty(method);
JsonElement Content(string path, string contentType)
    => Operation(path).GetProperty("requestBody").GetProperty("content").GetProperty(contentType);
foreach (var path in new[] { "/upload", "/upload-generic" })
{
    var file = Content(path, "multipart/form-data").GetProperty("encoding").GetProperty("File");
    if (!file.TryGetProperty("contentType", out var contentType) || contentType.GetString() != "image/png")
        throw new InvalidOperationException($"{path}: expected encoding.File.contentType=image/png, got {file}.");
    Console.WriteLine($"{path}: encoding.File.contentType=image/png");
}

#if SWASHBUCKLE_11
var reference = Content("/upload-reference", "multipart/form-data");
var referenceFile = reference.GetProperty("schema").GetProperty("properties").GetProperty("File");
if (referenceFile.GetProperty("description").GetString() != "Allowed content types: image/png."
    || reference.GetProperty("encoding").GetProperty("File").TryGetProperty("contentType", out _))
{
    throw new InvalidOperationException("The media type reference must contribute its schema without changing its encoding.");
}
Console.WriteLine("Reference media type: schema enriched, encoding unchanged.");
#endif

var form = Content("/form", "application/x-www-form-urlencoded");
if (form.GetProperty("schema").GetProperty("properties").GetProperty("Name").GetProperty("maxLength").GetInt32() != 7
    || (form.TryGetProperty("encoding", out var formEncoding)
        && formEncoding.GetProperty("Name").TryGetProperty("contentType", out _)))
{
    throw new InvalidOperationException("URL-encoded form must have Name.maxLength=7 without a per-field contentType.");
}
Console.WriteLine("URL-encoded form: Name.maxLength=7; no per-field contentType.");

if (Operation("/no-body", "get").TryGetProperty("requestBody", out _))
    throw new InvalidOperationException("GET /no-body unexpectedly has a request body.");
Console.WriteLine("GET /no-body: no requestBody.");

var length = root.GetProperty("components").GetProperty("schemas")
    .GetProperty("JsonRequest").GetProperty("properties").GetProperty("name").GetProperty("maxLength").GetInt32();
if (length != 7)
    throw new InvalidOperationException($"Expected JsonRequest.name.maxLength=7, got {length}.");
Console.WriteLine("JsonRequest.name.maxLength=7");
await app.StopAsync();

public sealed record JsonRequest(string Name);

public sealed class JsonRequestValidator : AbstractValidator<JsonRequest>
{
    public JsonRequestValidator() => RuleFor(request => request.Name).MaximumLength(7);
}

public sealed class UploadRequest
{
    public string? Caption { get; set; }

    public IFormFile File { get; set; } = null!;
}

public sealed class UploadRequestValidator : AbstractValidator<UploadRequest>
{
    public UploadRequestValidator() => RuleFor(request => request.File).FileContentType("image/png");
}

public sealed class FormRequest
{
    public string? Name { get; set; }
}

public sealed class FormRequestValidator : AbstractValidator<FormRequest>
{
    public FormRequestValidator() => RuleFor(request => request.Name).MaximumLength(7);
}

[ApiController]
public sealed class ProbeController : ControllerBase
{
    [HttpPost("/json")]
    public IActionResult PostJson([FromBody] JsonRequest request) => NoContent();

    [HttpGet("/no-body")]
    public IActionResult NoBody() => NoContent();

    [HttpPost("/form")]
    [Consumes("application/x-www-form-urlencoded")]
    public IActionResult PostForm([FromForm] FormRequest request) => NoContent();

    [HttpPost("/upload")]
    [HttpPost("/upload-generic")]
#if SWASHBUCKLE_11
    [HttpPost("/upload-reference")]
#endif
    public IActionResult Upload([FromForm] UploadRequest request) => NoContent();
}

public sealed class PrepareRequestBody : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.RelativePath == "upload-generic" && operation.RequestBody is OpenApiRequestBody genericBody)
        {
            genericBody.Content = GenericDictionaryProxy.Wrap(genericBody.Content!);
        }
#if SWASHBUCKLE_11
        if (context.ApiDescription.RelativePath == "upload-reference" && operation.RequestBody is OpenApiRequestBody referenceBody)
        {
            var target = referenceBody.Content!["multipart/form-data"];
            var document = new OpenApiDocument
            {
                Components = new OpenApiComponents
                {
                    MediaTypes = new Dictionary<string, MediaType> { ["Upload"] = target },
                },
            };
            document.RegisterComponents();
            referenceBody.Content["multipart/form-data"] = new OpenApiMediaTypeReference("Upload", document);
        }
#endif
    }
}

// This proxy exposes only the generic IDictionary interface and its inherited interfaces.
public class GenericDictionaryProxy : DispatchProxy
{
    private IDictionary<string, MediaType> _inner = null!;

    public static IDictionary<string, MediaType> Wrap(IDictionary<string, MediaType> inner)
    {
        var proxy = Create<IDictionary<string, MediaType>, GenericDictionaryProxy>();
        ((GenericDictionaryProxy)(object)proxy)._inner = inner;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod!.Invoke(_inner, args);
}
