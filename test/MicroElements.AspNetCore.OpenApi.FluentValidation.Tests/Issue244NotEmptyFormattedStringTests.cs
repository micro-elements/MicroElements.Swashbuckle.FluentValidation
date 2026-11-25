// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace MicroElements.AspNetCore.OpenApi.FluentValidation.Tests;

/// <summary>
/// NotEmpty on a value type serialized as a formatted string (<c>Guid</c> as uuid, <c>DateTime</c> as date-time, ...)
/// must not emit <c>minLength: 1</c>. For those types NotEmpty means "not the default value" (<c>Guid.Empty</c>,
/// <c>DateTime.MinValue</c>), which JSON Schema cannot express, and the serialized string is never empty anyway.
/// A plain string keeps <c>minLength: 1</c>.
/// </summary>
public class Issue244NotEmptyFormattedStringTests : IClassFixture<AspNetCoreOpenApiTests.TestWebApplicationFactory>
{
    private readonly AspNetCoreOpenApiTests.TestWebApplicationFactory _factory;

    public NotEmptyFormattedStringTests(AspNetCoreOpenApiTests.TestWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("personId", "uuid")]
    [InlineData("optionalPersonId", "uuid")]
    [InlineData("createdAt", "date-time")]
    [InlineData("updatedAt", "date-time")]
    [InlineData("birthDate", "date")]
    [InlineData("startTime", "time")]
    public async Task NotEmpty_on_formatted_value_type_should_not_emit_min_length(string propertyName, string format)
    {
        var properties = await GetPropertiesAsync();

        var property = properties.GetProperty(propertyName);
        property.GetProperty("format").GetString().Should().Be(format);
        property.TryGetProperty("minLength", out _).Should().BeFalse(
            because: "NotEmpty on a value type means 'not default', which minLength cannot express");
    }

    [Fact]
    public async Task NotEmpty_on_string_should_emit_min_length()
    {
        var properties = await GetPropertiesAsync();

        properties.GetProperty("name").GetProperty("minLength").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task NotEmpty_on_email_string_should_emit_min_length()
    {
        var properties = await GetPropertiesAsync();

        var email = properties.GetProperty("email");
        email.GetProperty("format").GetString().Should().Be("email");
        email.GetProperty("minLength").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task NotEmpty_on_formatted_value_type_should_still_mark_the_property_required()
    {
        using var client = _factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var required = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("TestNotEmptyFormattedModel").GetProperty("required")
            .EnumerateArray().Select(x => x.GetString()).ToList();

        required.Should().Contain(new[] { "personId", "optionalPersonId", "createdAt", "name" });
    }

    private async Task<JsonElement> GetPropertiesAsync()
    {
        using var client = _factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        return document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("TestNotEmptyFormattedModel").GetProperty("properties").Clone();
    }
}
