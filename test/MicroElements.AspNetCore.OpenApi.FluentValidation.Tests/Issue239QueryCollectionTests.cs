// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace MicroElements.AspNetCore.OpenApi.FluentValidation.Tests;

/// <summary>
/// Issue #239: a <c>[FromQuery]</c> collection property (<c>IReadOnlyList&lt;Guid&gt;?</c>) validated by a
/// custom <see cref="FluentValidation.Validators.ILengthValidator"/> (Min = 0, Max = 10) must produce
/// <c>maxItems: 10</c> on the array parameter schema — not <c>maxLength: 10</c>. The same DTO in
/// <c>[FromBody]</c> keeps its (already correct) constraints.
/// </summary>
public class Issue239QueryCollectionTests : IClassFixture<AspNetCoreOpenApiTests.TestWebApplicationFactory>
{
    private readonly AspNetCoreOpenApiTests.TestWebApplicationFactory _factory;

    public Issue239QueryCollectionTests(AspNetCoreOpenApiTests.TestWebApplicationFactory factory) => _factory = factory;

    /// <summary>
    /// The defining check for #239: the serialized OpenAPI document must show the array query parameter
    /// constrained by maxItems, with the element schema (string/uuid) untouched.
    /// </summary>
    [Fact]
    public async Task QueryIdsParameter_ShouldHaveMaxItemsInsteadOfMaxLength()
    {
        var parameters = await GetListQueryParametersAsync();

        var ids = FindParameter(parameters, "Ids");
        var schema = ids.GetProperty("schema");

        GetStringTypes(schema).Should().ContainSingle(t => t == "array", because: "Ids binds as an array query parameter");

        schema.GetProperty("maxItems").GetInt32().Should().Be(10);
        schema.TryGetProperty("maxLength", out _).Should().BeFalse(
            because: "a Length rule on a collection must produce maxItems, not maxLength (#239)");
        schema.TryGetProperty("minLength", out _).Should().BeFalse();
        schema.TryGetProperty("minItems", out _).Should().BeFalse(
            because: "the validator Min is 0, and 0 must not produce minItems");

        var items = schema.GetProperty("items");
        GetStringTypes(items).Should().ContainSingle(t => t == "string");
        items.GetProperty("format").GetString().Should().Be("uuid");
    }

    /// <summary>
    /// A string query property with a stock .Length(2, 7) keeps its string bounds: no minItems/maxItems.
    /// </summary>
    [Fact]
    public async Task QueryTextParameter_ShouldKeepStringLengthBounds()
    {
        var parameters = await GetListQueryParametersAsync();

        var text = FindParameter(parameters, "Text");
        var schema = text.GetProperty("schema");

        GetStringTypes(schema).Should().ContainSingle(t => t == "string");
        schema.GetProperty("minLength").GetInt32().Should().Be(2);
        schema.GetProperty("maxLength").GetInt32().Should().Be(7);
        schema.TryGetProperty("minItems", out _).Should().BeFalse();
        schema.TryGetProperty("maxItems", out _).Should().BeFalse();
    }

    /// <summary>
    /// Cache isolation: both parameters of one DTO are validated through one cached temporary schema;
    /// the resulting parameter schemas must not exchange constraints. Each parameter's schema is checked
    /// exhaustively over its constraint keys, so a leaked bound cannot hide.
    /// </summary>
    [Fact]
    public async Task QueryParameters_ShouldNotExchangeConstraints()
    {
        var parameters = await GetListQueryParametersAsync();

        // Ids: an array schema with ONLY maxItems among the constraint keys; Text's bounds (7, string
        // length keys) must not appear anywhere in it.
        var idsSchema = FindParameter(parameters, "Ids").GetProperty("schema");
        idsSchema.GetProperty("maxItems").GetInt32().Should().Be(10);
        ShouldNotHaveConstraintKeys(
            idsSchema,
            "minLength", "maxLength", "minItems", "minimum", "maximum", "exclusiveMinimum", "exclusiveMaximum", "pattern");

        // Text: a string schema with ONLY minLength/maxLength; Ids' bounds (10, items keys) must not appear.
        var textSchema = FindParameter(parameters, "Text").GetProperty("schema");
        textSchema.GetProperty("minLength").GetInt32().Should().Be(2);
        textSchema.GetProperty("maxLength").GetInt32().Should().Be(7);
        ShouldNotHaveConstraintKeys(
            textSchema,
            "minItems", "maxItems", "minimum", "maximum", "exclusiveMinimum", "exclusiveMaximum", "pattern");
    }

    /// <summary>
    /// F1: <c>RuleForEach(...).MaximumLength(10)</c> constrains the ELEMENTS, not the collection. The
    /// array query parameter must not gain <c>maxItems: 10</c> (nor the misplaced <c>maxLength</c> the
    /// pre-#239 base produced). The element-level constraint itself stays unpublished on query
    /// parameters — a pre-existing limitation outside this fix.
    /// </summary>
    [Fact]
    public async Task QueryItemRule_ShouldNotConstrainCollectionCount()
    {
        var parameters = await GetOperationParametersAsync("/api/item-rule-query");

        var words = FindParameter(parameters, "Words").GetProperty("schema");
        GetStringTypes(words).Should().ContainSingle(t => t == "array");
        words.TryGetProperty("maxItems", out _).Should().BeFalse(
            because: "an element length rule must not become a collection item count bound");
        words.TryGetProperty("maxLength", out _).Should().BeFalse();
        words.TryGetProperty("minItems", out _).Should().BeFalse();
        words.TryGetProperty("minLength", out _).Should().BeFalse();

        // The element schema stays framework-owned.
        var items = words.GetProperty("items");
        GetStringTypes(items).Should().ContainSingle(t => t == "string");
    }

    /// <summary>
    /// F2: a DataAnnotations <c>[MaxLength(3)]</c> on the collection reaches the parameter as
    /// <c>maxItems: 3</c>; the #239 length validator (Max = 10) must not weaken it to 10.
    /// Expect <c>maxItems = 3</c> without <c>maxLength</c>.
    /// </summary>
    [Fact]
    public async Task QueryAnnotatedCollection_ShouldKeepAnnotationMaxItems()
    {
        var parameters = await GetOperationParametersAsync("/api/annotated-list-query");

        var ids = FindParameter(parameters, "Ids").GetProperty("schema");
        ids.GetProperty("maxItems").GetInt32().Should().Be(3,
            because: "the DataAnnotations bound must survive the #239 validator instead of being overwritten");
        ids.TryGetProperty("maxLength", out _).Should().BeFalse();
        ids.TryGetProperty("minItems", out _).Should().BeFalse();

        var items = ids.GetProperty("items");
        GetStringTypes(items).Should().ContainSingle(t => t == "string");
        items.GetProperty("format").GetString().Should().Be("uuid");
    }

    /// <summary>
    /// The same DTO and validator in [FromBody] keep the (already correct) body constraints.
    /// </summary>
    [Fact]
    public async Task Body_ShouldKeepMaxItemsNullableAndUuidItems()
    {
        var schemas = await GetSchemasAsync();
        var properties = schemas.GetProperty("TestListQueryDto").GetProperty("properties");

        var ids = properties.GetProperty("ids");
        ids.GetProperty("maxItems").GetInt32().Should().Be(10);
        ids.TryGetProperty("maxLength", out _).Should().BeFalse(
            because: "the body path is already correct and must stay that way");
        IsNullable(ids).Should().BeTrue(because: "IReadOnlyList<Guid>? is nullable");
        var items = ids.GetProperty("items");
        GetStringTypes(items).Should().ContainSingle(t => t == "string");
        items.GetProperty("format").GetString().Should().Be("uuid");

        var text = properties.GetProperty("text");
        text.GetProperty("minLength").GetInt32().Should().Be(2);
        text.GetProperty("maxLength").GetInt32().Should().Be(7);
    }

    /// <summary>
    /// The document defects above are orthogonal to runtime validation: the custom validator itself
    /// enforces Min = 0 / Max = 10 on the collection (and the stock rule bounds Text).
    /// </summary>
    [Fact]
    public void RuntimeValidator_ShouldEnforceBoundsIndependently()
    {
        var validator = new TestListQueryDtoValidator();

        validator.Validate(new TestListQueryDto { Ids = Repeat(9) }).IsValid.Should().BeTrue();
        validator.Validate(new TestListQueryDto { Ids = Repeat(10) }).IsValid.Should().BeTrue();
        validator.Validate(new TestListQueryDto { Ids = Repeat(11) }).IsValid.Should().BeFalse();
        validator.Validate(new TestListQueryDto { Ids = Repeat(11), Text = "ok" }).IsValid.Should().BeFalse();

        validator.Validate(new TestListQueryDto { Text = "x" }).IsValid.Should().BeFalse();
        validator.Validate(new TestListQueryDto { Text = "ok" }).IsValid.Should().BeTrue();
        validator.Validate(new TestListQueryDto { Text = "okokokok" }).IsValid.Should().BeFalse();
    }

    private static Guid[] Repeat(int count) => Enumerable.Repeat(Guid.Empty, count).ToArray();

    private async Task<JsonElement> GetListQueryParametersAsync()
    {
        return await GetOperationParametersAsync("/api/list-query");
    }

    private async Task<JsonElement> GetOperationParametersAsync(string path)
    {
        var doc = await GetDocumentAsync();
        return doc.RootElement
            .GetProperty("paths")
            .GetProperty(path)
            .GetProperty("get")
            .GetProperty("parameters");
    }

    private async Task<JsonElement> GetSchemasAsync()
    {
        var doc = await GetDocumentAsync();
        return doc.RootElement.GetProperty("components").GetProperty("schemas");
    }

    private async Task<JsonDocument> GetDocumentAsync()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            // The tests never use cookies; a cookieless handler keeps the test host independent
            // of the environment's cookie-container behavior.
            HandleCookies = false,
        });
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static JsonElement FindParameter(JsonElement parameters, string name) => parameters
        .EnumerateArray()
        .First(p => string.Equals(p.GetProperty("name").GetString(), name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Asserts that none of the constraint keys exist on the schema.</summary>
    private static void ShouldNotHaveConstraintKeys(JsonElement schema, params string[] keys)
    {
        foreach (var key in keys)
        {
            schema.TryGetProperty(key, out _).Should().BeFalse(because: $"unexpected constraint key '{key}'");
        }
    }

    /// <summary>
    /// OpenAPI 3.0 (net9) types are strings; OpenAPI 3.1 (net10/net11) may use arrays of type names.
    /// </summary>
    private static List<string> GetStringTypes(JsonElement schema)
    {
        if (schema.TryGetProperty("type", out var type))
        {
            return type.ValueKind == JsonValueKind.Array
                ? type.EnumerateArray().Select(e => e.GetString()!).ToList()
                : new List<string> { type.GetString()! };
        }

        return new List<string>();
    }

    /// <summary>OpenAPI 3.0 uses nullable; OpenAPI 3.1 includes null in the type array.</summary>
    private static bool IsNullable(JsonElement property)
    {
        if (property.TryGetProperty("nullable", out var nullable) && nullable.GetBoolean())
            return true;

        return GetStringTypes(property).Contains("null");
    }
}