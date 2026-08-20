// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace MicroElements.AspNetCore.OpenApi.FluentValidation.Tests;

/// <summary>
/// Issue #232: FluentValidation rules were not applied to a <c>[FromForm]</c> DTO bound by an MVC controller
/// action. MVC ApiExplorer flattens the parameter into one form field per property and
/// Microsoft.AspNetCore.OpenApi emits an inline request-body schema from those descriptions, so
/// <c>FluentValidationSchemaTransformer</c> — which works off the DTO's JsonTypeInfo — never sees the type.
/// Minimal API form bodies are not flattened and were never affected (see <see cref="Issue216SpikeTests"/>).
/// </summary>
public class Issue232FormBodyTests : IClassFixture<AspNetCoreOpenApiTests.TestWebApplicationFactory>
{
    private const string UrlEncoded = "application/x-www-form-urlencoded";
    private const string Multipart = "multipart/form-data";

    private readonly AspNetCoreOpenApiTests.TestWebApplicationFactory _factory;

    public Issue232FormBodyTests(AspNetCoreOpenApiTests.TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task ControllerFromForm_UrlEncodedBody_HasValueConstraints()
    {
        var schema = await GetFormSchemaAsync("/api/issue232-form", UrlEncoded);
        var properties = schema.GetProperty("properties");

        // NotEmpty + MaximumLength(42)
        var name = properties.GetProperty("Name");
        name.GetProperty("minLength").GetInt32().Should().Be(1);
        name.GetProperty("maxLength").GetInt32().Should().Be(42);

        // GreaterThanOrEqualTo(7) + LessThanOrEqualTo(99)
        var age = properties.GetProperty("Age");
        age.GetProperty("minimum").GetInt32().Should().Be(7);
        age.GetProperty("maximum").GetInt32().Should().Be(99);
    }

    [Fact]
    public async Task ControllerFromForm_RequiredUsesSchemaKeys()
    {
        var schema = await GetFormSchemaAsync("/api/issue232-form", UrlEncoded);

        var required = schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToArray();
        required.Should().BeEquivalentTo(["Name"]);

        // Flattened form fields use the binding name (PascalCase), not the JSON naming policy. Deriving the
        // required entry from the name resolver instead of the schema's own keys would emit "name" here and
        // silently drop every constraint — a document naming a property that does not exist.
        var propertyKeys = schema.GetProperty("properties").EnumerateObject().Select(p => p.Name).ToArray();
        required.Should().BeSubsetOf(propertyKeys);
    }

    [Fact]
    public async Task ControllerFromForm_DoesNotIntroduceComponentSchema()
    {
        var document = await GetDocumentAsync();

        var componentNames = document.TryGetProperty("components", out var components)
                             && components.TryGetProperty("schemas", out var schemas)
            ? schemas.EnumerateObject().Select(p => p.Name).ToArray()
            : [];

        // The fix mutates the inline body schema in place; it must never register a component for a form DTO.
        componentNames.Should().NotContain("Issue232FormDto");
        componentNames.Should().NotContain("Issue232FileFormDto");
        componentNames.Should().NotContain("Issue232PatternFormDto");
        componentNames.Should().NotContain("Issue232ExclusiveFormDto");
        componentNames.Should().NotContain("Issue232NoValidatorFormDto");
    }

#if OPENAPI_V2
    [Fact]
    public async Task ControllerFromForm_PreservesFrameworkGeneratedFacets()
    {
        var schema = await GetFormSchemaAsync("/api/issue232-form", UrlEncoded);
        var age = schema.GetProperty("properties").GetProperty("Age");

        // net10 describes a form-bound int as a string/integer union with a numeric pattern. Applying the
        // numeric bounds must not clobber either facet.
        age.GetProperty("pattern").GetString().Should().Be(@"^-?(?:0|[1-9]\d*)$");
        age.GetProperty("type").EnumerateArray().Select(e => e.GetString())
            .Should().BeEquivalentTo(["integer", "string"]);
    }
#endif

    [Fact]
    public async Task ControllerFromForm_MultipartBody_AppliesConstraintsAndEmitsEncoding()
    {
        var mediaType = await GetFormMediaTypeAsync("/api/issue232-multipart", Multipart, "v2");
        var schema = mediaType.GetProperty("schema");
        var properties = schema.GetProperty("properties");

        properties.GetProperty("Title").GetProperty("maxLength").GetInt32().Should().Be(20);
        properties.GetProperty("Count").GetProperty("minimum").GetInt32().Should().Be(1);

        var required = schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToArray();
        required.Should().Contain("File");

        // Issue #216 encoding must keep working for the controller shape.
        // The File part itself is a $ref to the shared IFormFile component on net10 — assert only its key.
        var fileEncoding = mediaType.GetProperty("encoding").EnumerateObject()
            .First(property => property.Name.Equals("File", StringComparison.OrdinalIgnoreCase))
            .Value;
        fileEncoding.GetProperty("contentType").GetString().Should().Be("image/jpeg, image/png");
    }

    [Fact]
    public async Task ControllerFromForm_WithoutValidator_LeavesSchemaUntouched()
    {
        var schema = await GetFormSchemaAsync("/api/issue232-novalidator", UrlEncoded);

        schema.TryGetProperty("required", out _).Should().BeFalse();

        string[] constraintKeywords = ["minLength", "maxLength", "minimum", "maximum", "pattern"];
        foreach (var property in schema.GetProperty("properties").EnumerateObject())
        {
            foreach (var keyword in constraintKeywords)
            {
                // net10 emits a framework pattern for the int property — only the string one must stay bare.
                if (keyword == "pattern" && !property.Name.Equals("Note", StringComparison.Ordinal))
                    continue;

                property.Value.TryGetProperty(keyword, out _)
                    .Should().BeFalse($"'{property.Name}' has no validator and must not get '{keyword}'");
            }
        }
    }

    [Fact]
    public async Task ControllerFromForm_SingleMatchRule_IsNotDoubleWrapped()
    {
        var schema = await GetFormSchemaAsync("/api/issue232-pattern", UrlEncoded);

        // PatternCombiner detects an already-combined pattern by its prefix, so applying a single Matches()
        // rule twice would wrap it into a lookahead. This asserts the schema is processed exactly once.
        schema.GetProperty("properties").GetProperty("Single")
            .GetProperty("pattern").GetString().Should().Be("^[a-z]+$");
    }

    [Fact]
    public async Task ControllerFromForm_MultipleMatchRules_CombineIntoSinglePattern()
    {
        var schema = await GetFormSchemaAsync("/api/issue232-pattern", UrlEncoded);
        var triple = schema.GetProperty("properties").GetProperty("Triple");

        triple.GetProperty("pattern").GetString()
            .Should().Be(@"(?=[\s\S]*(?:[a-z]))(?=[\s\S]*(?:[A-Z]))(?=[\s\S]*(?:[0-9]))");
        triple.TryGetProperty("allOf", out _).Should().BeFalse();
    }

    [Fact]
    public async Task ControllerFromForm_ExclusiveComparison_IsWellFormed()
    {
        var schema = await GetFormSchemaAsync("/api/issue232-exclusive", UrlEncoded);
        var amount = schema.GetProperty("properties").GetProperty("Amount");

#if OPENAPI_V2
        // OpenAPI 3.1: exclusiveMinimum is the bound itself and minimum must be gone.
        amount.GetProperty("exclusiveMinimum").ToString().Should().Be("0");
        amount.TryGetProperty("minimum", out _).Should().BeFalse();
#else
        // OpenAPI 3.0: exclusiveMinimum is a boolean modifier on minimum.
        amount.GetProperty("exclusiveMinimum").GetBoolean().Should().BeTrue();
        amount.GetProperty("minimum").GetInt32().Should().Be(0);
#endif
    }

    [Fact]
    public async Task ControllerFromForm_DottedNestedKey_DoesNotBorrowRulesFromAFlatProperty()
    {
        var schema = await GetFormSchemaAsync("/api/issue232-nested", UrlEncoded);
        var properties = schema.GetProperty("properties");

        // The rule matcher ignores non-alphanumeric characters, so "Inner.City" reduces to the same token as
        // the unrelated root property "InnerCity" and would silently inherit its constraints.
        var nested = properties.GetProperty("Inner.City");
        nested.TryGetProperty("minLength", out _).Should().BeFalse();
        nested.TryGetProperty("maxLength", out _).Should().BeFalse();

        var flat = properties.GetProperty("InnerCity");
        flat.GetProperty("minLength").GetInt32().Should().Be(1);
        flat.GetProperty("maxLength").GetInt32().Should().Be(7);

        schema.GetProperty("required").EnumerateArray().Select(e => e.GetString())
            .Should().BeEquivalentTo(["InnerCity"]);
    }

    [Fact]
    public async Task ControllerFromForm_MultipleFormParameters_ConstrainEveryAllOfPropertyBag()
    {
        var schema = await GetFormSchemaAsync("/api/issue232-multiparam", UrlEncoded);

        // Two form parameters compose the body as an allOf of one property bag per parameter, so the body
        // schema itself carries no properties at all.
        schema.TryGetProperty("properties", out _).Should().BeFalse();

        var bags = schema.GetProperty("allOf").EnumerateArray()
            .SelectMany(bag => bag.GetProperty("properties").EnumerateObject())
            .ToDictionary(property => property.Name, property => property.Value);

        bags["Alpha"].GetProperty("maxLength").GetInt32().Should().Be(3);
        bags["Beta"].GetProperty("maxLength").GetInt32().Should().Be(4);
    }

    [Fact]
    public async Task ControllerFromForm_CollidingPropertyNames_StayWithTheirOwnParameter()
    {
        var schema = await GetFormSchemaAsync("/api/issue232-colliding", UrlEncoded);
        var bags = schema.GetProperty("allOf").EnumerateArray().ToArray();

        // The rule matcher compares property names only, so applying every parameter's validator to every bag
        // would hand the left DTO's constraints to the right one and vice versa.
        var left = bags[0];
        left.GetProperty("properties").GetProperty("Name").GetProperty("minLength").GetInt32().Should().Be(1);
        left.GetProperty("properties").GetProperty("Name").GetProperty("maxLength").GetInt32().Should().Be(5);
        left.GetProperty("required").EnumerateArray().Select(e => e.GetString()).Should().BeEquivalentTo(["Name"]);

        var right = bags[1];
        var rightName = right.GetProperty("properties").GetProperty("Name");
        rightName.GetProperty("maxLength").GetInt32().Should().Be(100);
        rightName.TryGetProperty("minLength", out _).Should().BeFalse();
        right.TryGetProperty("required", out _).Should().BeFalse();
    }

    [Fact]
    public async Task ControllerFromForm_IncludedValidatorRules_ReachTheFormSchema()
    {
        var schema = await GetFormSchemaAsync("/api/issue232-include", UrlEncoded);

        var shared = schema.GetProperty("properties").GetProperty("Shared");
        shared.GetProperty("minLength").GetInt32().Should().Be(1);
        shared.GetProperty("maxLength").GetInt32().Should().Be(11);

        schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()).Should().Contain("Shared");
    }

    [Fact]
    public async Task MinimalApiFormBody_IsNotProcessedByTheOperationTransformer()
    {
        // Pins the ContainerType == null discriminator: a minimal API form parameter is not flattened and is
        // owned by the schema transformer. Processing it here too would re-apply rules to a shared component —
        // PatternCombiner would double-wrap an already combined pattern, among others.
        var document = await GetDocumentAsync();
        var upload = document.GetProperty("paths").GetProperty("/api/upload")
            .GetProperty("post").GetProperty("requestBody").GetProperty("content");

        foreach (var contentType in upload.EnumerateObject())
        {
            var schema = contentType.Value.GetProperty("schema");

            // The body stays a reference to the DTO component; it never becomes an inline property bag.
            schema.TryGetProperty("properties", out _).Should().BeFalse();
            schema.TryGetProperty("required", out _).Should().BeFalse();
        }
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

    private async Task<JsonElement> GetFormMediaTypeAsync(string path, string contentType, string documentName = "v1")
    {
        var document = await GetDocumentAsync(documentName);
        return document.GetProperty("paths").GetProperty(path)
            .GetProperty("post").GetProperty("requestBody")
            .GetProperty("content").GetProperty(contentType);
    }

    private async Task<JsonElement> GetFormSchemaAsync(string path, string contentType)
    {
        var mediaType = await GetFormMediaTypeAsync(path, contentType);
        return mediaType.GetProperty("schema");
    }
}
