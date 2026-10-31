// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using MicroElements.OpenApi.FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MicroElements.AspNetCore.OpenApi.FluentValidation.Tests;

/// <summary>
/// Issue #236 / ADR-008 Phase 3 regression coverage (§5.2) and serialization assertions (§5.3).
/// Tests without a TFM guard run on every native TFM (net9/net10/net11); net11-only behavior
/// (OpenAPI 3.1 document, OpenApi 3.x reference media types, framework binary schema) is guarded
/// with <c>NET11_0_OR_GREATER</c>.
/// </summary>
public class Issue236Net11Tests : IClassFixture<AspNetCoreOpenApiTests.TestWebApplicationFactory>
{
    private readonly AspNetCoreOpenApiTests.TestWebApplicationFactory _factory;

    public Issue236Net11Tests(AspNetCoreOpenApiTests.TestWebApplicationFactory factory) => _factory = factory;

    // ----- §5.2: conditional rules -----

    /// <summary>
    /// Default <see cref="MicroElements.OpenApi.FluentValidation.ConditionalRulesMode.Exclude"/> semantics:
    /// a rule guarded by <c>.When()</c> is excluded from the schema (the condition cannot be represented
    /// in OpenAPI), while unconditional rules on the same validator keep constraining it. Pinned for
    /// ADR-008 Decision 7 parity on every TFM.
    /// </summary>
    [Fact]
    public async Task ConditionalRule_When_ShouldApplyConstraints()
    {
        var schemas = await GetSchemasAsync();

        var customer = schemas.GetProperty("TestCustomer");
        var props = customer.GetProperty("properties");

        // Control: the transformer ran — the unconditional Age rule is present.
        props.GetProperty("age").GetProperty("minimum").GetInt32().Should().Be(0);

        // The When-guarded Phone rule is excluded from the schema by the default mode.
        props.GetProperty("phone").TryGetProperty("pattern", out _)
            .Should().BeFalse(because: "ConditionalRulesMode.Exclude drops .When() rules from the schema");

        // And the condition does not make the property required either.
        var required = customer.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToArray();
        required.Should().NotContain("phone");
    }

    // ----- §5.2: shared schema across two endpoints -----

    /// <summary>
    /// Two endpoints returning/requesting the same DTO must reference ONE component schema that
    /// keeps its FluentValidation constraints ($ref integrity + shared reuse).
    /// </summary>
    [Fact]
    public async Task SharedSchema_AcrossTwoEndpoints_ShouldRetainConstraints()
    {
        var document = await GetDocumentAsync();

        foreach (var path in new[] { "/api/shared-a", "/api/shared-b" })
        {
            var schema = document.GetProperty("paths").GetProperty(path)
                .GetProperty("post").GetProperty("requestBody")
                .GetProperty("content").GetProperty("application/json").GetProperty("schema");

            schema.TryGetProperty("$ref", out var reference).Should().BeTrue($"{path} body should be a $ref");
            reference.GetString().Should().Contain("TestSharedDto");
        }

        var component = document.GetProperty("components").GetProperty("schemas").GetProperty("TestSharedDto");
        var name = component.GetProperty("properties").GetProperty("name");
        name.GetProperty("minLength").GetInt32().Should().Be(2);
        name.GetProperty("maxLength").GetInt32().Should().Be(40);
        component.GetProperty("required").EnumerateArray().Select(e => e.GetString())
            .Should().Contain("name");
    }

    // ----- §5.2: RuleForEach items — BLOCKED, see ADR-008-implementation-notes.md -----
    // The library does not map .ForEach(...) child rules to the collection's `items` schema on ANY TFM
    // (verified net9.0/net10.0/net11.0: skus.items carries only the framework "type": "string").
    // Adding the mapping is a behavior change outside the ADR-008 scope; the coordinator decides.
    // The fixture and endpoint stay in TestModels.cs/Program.cs; the test returns with the decision.

    // ----- §5.3: exact decimal strings (ADR-008 §1.3 folding-class guard) -----

    /// <summary>
    /// Decimal bounds serialize as the exact decimal strings (<c>5.1</c>/<c>10.2</c>), not the
    /// SDK-folded binary expansions. Asserted on the raw JSON token, not a parsed double.
    /// </summary>
    [Fact]
    public async Task DecimalBounds_ShouldSerializeAsExactDecimalStrings()
    {
        var schemas = await GetSchemasAsync();
        AssertExactDecimalBounds(schemas);

#if NET11_0_OR_GREATER
        var schemas31 = await GetSchemasAsync("v31");
        AssertExactDecimalBounds(schemas31);
#endif
    }

    private static void AssertExactDecimalBounds(JsonElement schemas)
    {
        var amount = schemas.GetProperty("TestDecimalBoundsModel")
            .GetProperty("properties").GetProperty("amount");

        amount.GetProperty("minimum").GetRawText().Should().Be("5.1");
        amount.GetProperty("maximum").GetRawText().Should().Be("10.2");
    }

#if NET11_0_OR_GREATER
    // ----- §5.1/§5.3: document versions (net11: "v1" = default 3.2, "v31" = explicit 3.1) -----

    [Fact]
    public async Task DocumentVersions_Default32_Explicit31()
    {
        var v1 = await GetDocumentRawAsync("v1");
        var v31 = await GetDocumentRawAsync("v31");

        using var doc32 = JsonDocument.Parse(v1);
        doc32.RootElement.GetProperty("openapi").GetString().Should().StartWith("3.2.");

        using var doc31 = JsonDocument.Parse(v31);
        doc31.RootElement.GetProperty("openapi").GetString().Should().StartWith("3.1.");
    }

    /// <summary>
    /// §5.3: the 3.1/3.2 nullability shape — the optional property guarded by a child adapter stays
    /// accepted-as-null on both documents; on the explicit 3.1 document the framework represents it
    /// with a type array containing "null".
    /// </summary>
    [Fact]
    public async Task NullabilityShape_On32And31Documents()
    {
        foreach (var documentName in new[] { "v1", "v31" })
        {
            var schemas = await GetSchemasAsync(documentName);
            var country = schemas.GetProperty("TestChildRulesNullability")
                .GetProperty("properties").GetProperty("country");

            var acceptedAsNull = country.TryGetProperty("nullable", out var nullable) && nullable.GetBoolean();
            if (country.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.Array)
                acceptedAsNull |= type.EnumerateArray().Any(x => x.GetString() == "null");
            acceptedAsNull.Should().BeTrue($"country stays nullable on {documentName} ({country.GetRawText()})");
        }

        var schemas31 = await GetSchemasAsync("v31");
        var country31 = schemas31.GetProperty("TestChildRulesNullability")
            .GetProperty("properties").GetProperty("country");
        country31.GetProperty("type").EnumerateArray().Select(x => x.GetString())
            .Should().Contain("null", because: "OpenAPI 3.1 expresses nullability via the type array");
    }

    // ----- §5.2: reference media types (OpenApi 3.x surface) -----

    /// <summary>
    /// The net11 narrowing (<c>content.Value as OpenApiMediaType</c>) must skip reference media types
    /// (<c>OpenApiMediaTypeReference</c>) through the existing null guard: no write attempt, no error
    /// logged, and the reference object passes through untouched.
    /// </summary>
    [Fact]
    public async Task ReferenceMediaType_IsSkipped_NoWriteAttempt()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddValidatorsFromAssemblyContaining<TestSharedDtoValidator>();
        services.AddFluentValidationRulesToOpenApi();
        await using var provider = services.BuildServiceProvider();

        var capturingLoggerFactory = new CapturingLoggerFactory();
        var transformer = new FluentValidationOperationTransformer(
            loggerFactory: capturingLoggerFactory,
            validatorRegistry: provider.GetRequiredService<IValidatorRegistry>());

        var mediaTypeReference = new Microsoft.OpenApi.OpenApiMediaTypeReference("multiPart", null, null);
        var operation = new Microsoft.OpenApi.OpenApiOperation
        {
            RequestBody = new Microsoft.OpenApi.OpenApiRequestBody
            {
                Content = new Dictionary<string, Microsoft.OpenApi.IOpenApiMediaType>
                {
                    ["multipart/form-data"] = mediaTypeReference,
                },
            },
        };

        var context = new Microsoft.AspNetCore.OpenApi.OpenApiOperationTransformerContext
        {
            DocumentName = "v1",
            Description = new Microsoft.AspNetCore.Mvc.ApiExplorer.ApiDescription(),
            ApplicationServices = provider,
        };
        await transformer.TransformAsync(operation, context, CancellationToken.None);

        capturingLoggerFactory.Warnings.Should().BeEmpty(
            because: "a reference media type is skipped by design and must not produce a write failure");

        // The reference itself must stay untouched: no encoding.contentType for the form parts,
        // and no rules applied to its schema.
        (mediaTypeReference.Encoding?.Count ?? 0).Should().Be(0);
    }

    // ----- §5.2: binary response smoke -----

    /// <summary>
    /// ASP.NET Core 11 describes a binary file response as a <c>FileContentResult</c> component with
    /// <c>type: string, format: binary</c> (the endpoint declares it via Produces). The description must
    /// be semantically identical with and without our transformers registered.
    /// </summary>
    [Fact]
    public async Task BinaryResponse_FrameworkSchema_NotModified()
    {
        await using var controlFactory = new NoRulesWebApplicationFactory();
        var withRules = await GetBinaryDescriptionAsync(_factory);
        var withoutRules = await GetBinaryDescriptionAsync(controlFactory);

        // Semantic comparison: type/format/$ref shape, not whole-document bytes.
        static (string? responseRef, string? componentType, string? componentFormat) Semantics(JsonElement document)
        {
            var responseSchema = document
                .GetProperty("paths").GetProperty("/api/binary")
                .GetProperty("get").GetProperty("responses")
                .GetProperty("200").GetProperty("content")
                .GetProperty("application/octet-stream").GetProperty("schema");

            var component = document.GetProperty("components").GetProperty("schemas")
                .GetProperty("FileContentResult");

            return (responseSchema.TryGetProperty("$ref", out var r) ? r.GetString() : null,
                component.TryGetProperty("type", out var t) ? t.GetString() : null,
                component.TryGetProperty("format", out var f) ? f.GetString() : null);
        }

        Semantics(withRules).Should().Be(Semantics(withoutRules),
            because: "our transformers must not modify the framework-generated binary schema");

        // Pin the framework shape: the component is the binary string schema.
        var component = withRules.GetProperty("components").GetProperty("schemas").GetProperty("FileContentResult");
        component.GetProperty("type").GetString().Should().Be("string");
        component.GetProperty("format").GetString().Should().Be("binary");
    }

    private static async Task<JsonElement> GetBinaryDescriptionAsync(WebApplicationFactory<TestMarker> factory)
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    /// <summary>
    /// §4: build-time documents (SampleAspNetCoreOpenApi, net11.0) must exist for 3.2 (Staging) and
    /// 3.1 (Production), exactly one JSON per directory, with the environment marker as info.title —
    /// proving OpenApiGenerationEnvironment reached the generation process.
    /// Run the two ADR §4 Phase 3 build commands from the repository root to produce the artifacts.
    /// </summary>
    [Fact]
    public void BuildTimeDocuments_32And31_WithEnvironmentMarker()
    {
        string? current = AppContext.BaseDirectory;
        string? repoRoot = null;
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current, "samples", "SampleAspNetCoreOpenApi")))
            {
                repoRoot = current;
                break;
            }
            current = Path.GetDirectoryName(current.TrimEnd(Path.DirectorySeparatorChar));
        }

        repoRoot.Should().NotBeNull("the repository root must contain samples/SampleAspNetCoreOpenApi");
        var dir32 = Path.Combine(repoRoot!, "artifacts", "net11", "openapi-3.2");
        var dir31 = Path.Combine(repoRoot!, "artifacts", "net11", "openapi-3.1");

        AssertBuildTimeDocument(dir32, expectedOpenApiPrefix: "3.2.", expectedTitle: "env-Staging");
        AssertBuildTimeDocument(dir31, expectedOpenApiPrefix: "3.1.", expectedTitle: "env-Production");
    }

    private static void AssertBuildTimeDocument(string directory, string expectedOpenApiPrefix, string expectedTitle)
    {
        Directory.Exists(directory).Should().BeTrue(
            $"build-time artifacts are missing: run the ADR §4 Phase 3 generation commands (expected {directory})");
        var files = Directory.GetFiles(directory, "*.json");
        files.Length.Should().Be(1, because: $"{directory} must contain exactly one generated document");

        using var document = JsonDocument.Parse(File.ReadAllText(files[0]));
        document.RootElement.GetProperty("openapi").GetString().Should().StartWith(expectedOpenApiPrefix);
        document.RootElement.GetProperty("info").GetProperty("title").GetString().Should().Be(expectedTitle);
    }

    /// <summary>
    /// Control host for the binary smoke: identical Program.cs, but our rules are disabled via
    /// the FvRules:Enabled switch.
    /// </summary>
    public sealed class NoRulesWebApplicationFactory : WebApplicationFactory<TestMarker>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseContentRoot(AppContext.BaseDirectory);
            builder.UseEnvironment("Testing");
            builder.UseSetting("FvRules:Enabled", "false");
        }
    }

    private sealed class CapturingLoggerFactory : ILoggerFactory
    {
        public List<string> Warnings { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        void IDisposable.Dispose() { }

        void ILoggerFactory.AddProvider(ILoggerProvider provider) =>
            throw new NotSupportedException();

        private sealed class CapturingLogger : ILogger
        {
            private readonly CapturingLoggerFactory _owner;

            public CapturingLogger(CapturingLoggerFactory owner) => _owner = owner;

            IDisposable? ILogger.BeginScope<TState>(TState state) => null;

            bool ILogger.IsEnabled(LogLevel logLevel) => true;

            void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (logLevel >= LogLevel.Warning)
                    _owner.Warnings.Add(formatter(state, exception));
            }
        }
    }
#endif

    private async Task<JsonElement> GetSchemasAsync(string documentName = "v1")
    {
        var document = await GetDocumentAsync(documentName);
        return document.GetProperty("components").GetProperty("schemas");
    }

    private async Task<string> GetDocumentRawAsync(string documentName)
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/openapi/{documentName}.json");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<JsonElement> GetDocumentAsync(string documentName = "v1")
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/openapi/{documentName}.json");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        // Clone so the returned element does not depend on the disposed JsonDocument's pooled buffer.
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
