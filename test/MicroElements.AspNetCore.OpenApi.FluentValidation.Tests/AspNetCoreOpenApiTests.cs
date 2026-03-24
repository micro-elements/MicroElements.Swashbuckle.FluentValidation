// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using MicroElements.AspNetCore.OpenApi.FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MicroElements.AspNetCore.OpenApi.FluentValidation.Tests;

public class AspNetCoreOpenApiTests : IClassFixture<AspNetCoreOpenApiTests.TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AspNetCoreOpenApiTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task BasicValidationRules_ShouldAppearInOpenApiDocument()
    {
        var schemas = await GetSchemasAsync();

        // Customer schema should have validation constraints
        var customer = schemas.GetProperty("TestCustomer");
        var props = customer.GetProperty("properties");

        // NotEmpty + MaximumLength => minLength: 1, maxLength: 50
        var firstName = props.GetProperty("firstName");
        firstName.GetProperty("minLength").GetInt32().Should().Be(1);
        firstName.GetProperty("maxLength").GetInt32().Should().Be(50);

        // GreaterThanOrEqualTo(0), LessThanOrEqualTo(150) => minimum: 0, maximum: 150
        var age = props.GetProperty("age");
        age.GetProperty("minimum").GetInt32().Should().Be(0);
        age.GetProperty("maximum").GetInt32().Should().Be(150);

        // Email => format: "email"
        var email = props.GetProperty("email");
        email.GetProperty("format").GetString().Should().Be("email");

        // Required array should contain firstName, lastName, email
        var required = customer.GetProperty("required");
        var requiredValues = required.EnumerateArray().Select(e => e.GetString()).ToArray();
        requiredValues.Should().Contain("firstName");
        requiredValues.Should().Contain("lastName");
        requiredValues.Should().Contain("email");
    }

    [Fact]
    public async Task EnumProperty_ShouldNotCauseErrors()
    {
        var schemas = await GetSchemasAsync();

        // The CustomerType enum should exist and Customer should reference it
        schemas.TryGetProperty("TestCustomerType", out _).Should().BeTrue();

        // Customer should still have validation constraints despite enum property
        var customer = schemas.GetProperty("TestCustomer");
        var props = customer.GetProperty("properties");
        props.GetProperty("firstName").GetProperty("minLength").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task ComparisonRules_ShouldApplyCorrectly()
    {
        var schemas = await GetSchemasAsync();

        var order = schemas.GetProperty("TestOrder");
        var amount = order.GetProperty("properties").GetProperty("amount");

        // OpenAPI 3.0 (net9.0): exclusiveMinimum is boolean true + minimum: 0
        // OpenAPI 3.1 (net10.0): exclusiveMinimum is a number (0)
        var exclusiveMin = amount.GetProperty("exclusiveMinimum");
        if (exclusiveMin.ValueKind == JsonValueKind.True)
            amount.GetProperty("minimum").GetInt32().Should().Be(0);
        else
            exclusiveMin.GetInt32().Should().Be(0);

        // Order.Quantity: InclusiveBetween(1, 1000) => minimum: 1, maximum: 1000
        var quantity = order.GetProperty("properties").GetProperty("quantity");
        quantity.GetProperty("minimum").GetInt32().Should().Be(1);
        quantity.GetProperty("maximum").GetInt32().Should().Be(1000);
    }

    /// <summary>
    /// Issue #146: BigInteger properties should have validation constraints applied.
    /// </summary>
    [Fact]
    public async Task BigIntegerProperty_ShouldHaveValidationConstraints()
    {
        var schemas = await GetSchemasAsync();

        var bigIntModel = schemas.GetProperty("TestBigIntegerModel");
        var props = bigIntModel.GetProperty("properties");

        // Name should have standard string constraints
        var name = props.GetProperty("name");
        name.GetProperty("minLength").GetInt32().Should().Be(1);
        name.GetProperty("maxLength").GetInt32().Should().Be(100);

        // Value (BigInteger): InclusiveBetween(0, 999) => minimum: 0, maximum: 999
        // In ASP.NET Core OpenAPI, BigInteger is serialized as a $ref to the component schema.
        // The transformer applies validation rules to the shared component schema object,
        // so constraints appear on the BigInteger component rather than inline on the property.
        var value = props.GetProperty("value");
        if (value.TryGetProperty("$ref", out _))
        {
            // Follow the $ref — constraints are on the BigInteger component schema
            var bigInteger = schemas.GetProperty("BigInteger");
            bigInteger.GetProperty("minimum").GetInt32().Should().Be(0);
            bigInteger.GetProperty("maximum").GetInt32().Should().Be(999);
        }
        else
        {
            value.GetProperty("minimum").GetInt32().Should().Be(0);
            value.GetProperty("maximum").GetInt32().Should().Be(999);
        }
    }

    /// <summary>
    /// Issue #200: Query parameters with [AsParameters] should have validation constraints.
    /// </summary>
    [Fact]
    public async Task QueryParameters_WithAsParameters_ShouldHaveValidationConstraints()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        // Find the /api/search GET operation
        var searchPath = doc.RootElement.GetProperty("paths").GetProperty("/api/search").GetProperty("get");
        var parameters = searchPath.GetProperty("parameters");

        // Find Skip and Take parameters
        JsonElement? skipParam = null, takeParam = null;
        foreach (var param in parameters.EnumerateArray())
        {
            var name = param.GetProperty("name").GetString();
            if (name == "Skip") skipParam = param;
            if (name == "Take") takeParam = param;
        }

        skipParam.Should().NotBeNull("Skip parameter should exist");
        takeParam.Should().NotBeNull("Take parameter should exist");

        // Skip: GreaterThanOrEqualTo(0) => minimum: 0
        var skipSchema = skipParam!.Value.GetProperty("schema");
        skipSchema.GetProperty("minimum").GetInt32().Should().Be(0);

        // Take: InclusiveBetween(1, 100) => minimum: 1, maximum: 100
        var takeSchema = takeParam!.Value.GetProperty("schema");
        takeSchema.GetProperty("minimum").GetInt32().Should().Be(1);
        takeSchema.GetProperty("maximum").GetInt32().Should().Be(100);
    }

    /// <summary>
    /// Issue #200 (part 2): Nested DTOs in request body should have validation constraints.
    /// Known limitation: schema transformer does not apply rules to nested component schemas yet.
    /// </summary>
    [Fact(Skip = "Known limitation: nested component schemas need separate investigation")]
    public async Task NestedDto_ShouldHaveValidationConstraints()
    {
        var schemas = await GetSchemasAsync();

        var createAccount = schemas.GetProperty("TestCreateAccount");
        var props = createAccount.GetProperty("properties");

        var email = props.GetProperty("email");
        email.GetProperty("minLength").GetInt32().Should().Be(1);
        email.GetProperty("format").GetString().Should().Be("email");

        var username = props.GetProperty("username");
        username.GetProperty("minLength").GetInt32().Should().Be(1);
        username.GetProperty("maxLength").GetInt32().Should().Be(50);
    }

    [Fact]
    public void TransformerCanResolveWithoutScope()
    {
        // Simulates build-time document generation (no HTTP scope)
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddValidatorsFromAssemblyContaining<TestCustomerValidator>();
        services.AddFluentValidationRulesToOpenApi();

        var provider = services.BuildServiceProvider();

        // Should not throw — Transient registration works without scope
        var transformer = provider.GetRequiredService<FluentValidationSchemaTransformer>();
        transformer.Should().NotBeNull();
    }

    private async Task<JsonElement> GetSchemasAsync()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("components").GetProperty("schemas");
    }

    // ----- Test App -----

    public class TestWebApplicationFactory : WebApplicationFactory<TestMarker>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseContentRoot(AppContext.BaseDirectory);
            builder.UseEnvironment("Testing");
        }
    }
}
