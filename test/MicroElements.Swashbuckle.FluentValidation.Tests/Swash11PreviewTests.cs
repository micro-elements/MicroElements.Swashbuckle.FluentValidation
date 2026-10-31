// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// EXPERIMENT (experiment/issue-236-swash11-openapi32): Swashbuckle 11 preview (PR #3786,
// Microsoft.OpenApi 3.x) on net11.0 with an OpenAPI 3.2 opt-in. This is an experiment row only —
// not a stable-release compatibility claim.

#if NET11_0_OR_GREATER
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace MicroElements.Swashbuckle.FluentValidation.Tests;

/// <summary>
/// Real end-to-end Swashbuckle generation on net11: SwaggerGen 11 preview generates the document for
/// the existing SampleWebApi host and the Swagger middleware serializes it as OpenAPI 3.2 (opt-in
/// <c>UseSwagger(o => o.OpenApiVersion = OpenApiSpecVersion.OpenApi3_2)</c> — PR #3786 docs).
/// One meaningful end-to-end check: HTTP 3.2 + FluentValidation constraints + loaded-package
/// provenance.
/// </summary>
public class Swash11PreviewTests
{
    [Fact]
    public async Task SwaggerMiddleware_ServesOpenApi32_WithFluentValidationConstraints()
    {
        // Provenance: the loaded SwaggerGen assembly is the pinned preview build (the version string
        // was read from the real assembly metadata; full value
        // 11.0.0-pr.3786.3260+e651d02ed63d75f10a0b3f3fc3d337ee430c999b).
        var swaggerGen = typeof(global::Swashbuckle.AspNetCore.SwaggerGen.SchemaGenerator).Assembly;
        swaggerGen.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            .Should().StartWith("11.0.0-pr.3786.3260");

        await using var factory = new WebApplicationFactory<SampleWebApi.Program>();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        // OpenAPI 3.2 opt-in through the Swagger middleware.
        root.GetProperty("openapi").GetString().Should().StartWith("3.2.");

        var schemas = root.GetProperty("components").GetProperty("schemas");

        // required: Customer.forename/surname (NotEmpty).
        var customer = schemas.GetProperty("Customer");
        var required = customer.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToArray();
        required.Should().Contain("forename");
        required.Should().Contain("surname");

        // length: Customer.forename (NotEmpty -> minLength 1; the sample validator sets no maximum).
        customer.GetProperty("properties").GetProperty("forename")
            .GetProperty("minLength").GetInt32().Should().Be(1);

        // length + nullable shape: Customer.address (CustomerAddressValidator via Include:
        // Length(20, 250), nullable string -> type array with "null" on OpenAPI 3.x).
        var address = customer.GetProperty("properties").GetProperty("address");
        address.GetProperty("minLength").GetInt32().Should().Be(20);
        address.GetProperty("maxLength").GetInt32().Should().Be(250);
        address.GetProperty("type").EnumerateArray().Select(e => e.GetString())
            .Should().Contain("null");

        // numeric bounds: Customer.discount (ExclusiveBetween(4, 5) -> numeric exclusive form on 3.x).
        var discount = customer.GetProperty("properties").GetProperty("discount");
        discount.GetProperty("exclusiveMinimum").GetInt32().Should().Be(4);
        discount.GetProperty("exclusiveMaximum").GetInt32().Should().Be(5);
        discount.TryGetProperty("minimum", out _).Should().BeFalse();
        discount.TryGetProperty("maximum", out _).Should().BeFalse();

        // length + merged pattern: PhoneEntity.MobilePhoneNumber (Matches combination + lengths).
        var phone = schemas.GetProperty("PhoneEntity").GetProperty("properties")
            .GetProperty("mobilePhoneNumber");
        phone.GetProperty("minLength").GetInt32().Should().Be(10);
        phone.GetProperty("maxLength").GetInt32().Should().Be(10);
        phone.GetProperty("pattern").GetString()
            .Should().Be("(?=[\\s\\S]*(?:^3))(?=[\\s\\S]*(?:^\\d*$))");

        // shared $ref: Dict.type stays a reference to the shared DataType component.
        schemas.GetProperty("Dict").GetProperty("properties").GetProperty("type")
            .GetProperty("$ref").GetString().Should().Contain("DataType");
    }
}
#endif
