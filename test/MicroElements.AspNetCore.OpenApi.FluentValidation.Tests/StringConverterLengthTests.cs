// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Validators;
using MicroElements.OpenApi.FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
#if OPENAPI_V2
using Microsoft.OpenApi;
#else
using Microsoft.OpenApi.Models;
#endif

namespace MicroElements.AspNetCore.OpenApi.FluentValidation.Tests;

public class StringConverterLengthTests
{
    [Theory]
    [InlineData("integer")]
    [InlineData("object")]
    public void Explicit_non_string_schema_should_not_receive_string_length(string schemaType)
    {
        var validator = new InlineValidator<TestNullableLengthRequest>();
        validator.RuleFor(x => x.Description).MaximumLength(500);
        var property = new OpenApiSchema();
#if OPENAPI_V2
        property.Type = Enum.Parse<JsonSchemaType>(schemaType, ignoreCase: true);
#else
        property.Type = schemaType;
#endif
        ApplyLength(validator, property, validator.CreateDescriptor().Rules.Single().Components.Single().Validator);
        property.MaxLength.Should().BeNull();
        property.MinLength.Should().BeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Untyped_non_string_value_should_not_receive_lifted_child_string_length(bool dictionary)
    {
        var validator = new InlineValidator<NonStringRequest>();
        if (dictionary)
            validator.RuleFor(x => x.Values).NotEmpty();
        else
            validator.RuleFor(x => x.Count).NotEmpty();

        var property = new OpenApiSchema();
        // Identity child NotEmpty is projected as MinimumLengthValidator<object>(1).
        ApplyLength(validator, property, new MinimumLengthValidator<object>(1));
        property.MinLength.Should().BeNull();
        property.MaxLength.Should().BeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task String_converter_should_preserve_length_rules_before_schema_type_is_restored(bool restoreType)
    {
        using var factory = CreateFactory(restoreType);
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var properties = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("TestNullableLengthRequest").GetProperty("properties");

        properties.GetProperty("description").GetProperty("maxLength").GetInt32().Should().Be(500);
        properties.GetProperty("description").GetProperty("minLength").GetInt32().Should().Be(10);
        properties.GetProperty("code").GetProperty("minLength").GetInt32().Should().Be(3);
        properties.GetProperty("name").GetProperty("minLength").GetInt32().Should().Be(2);
        properties.GetProperty("name").GetProperty("maxLength").GetInt32().Should().Be(50);
    }

    [Fact]
    public async Task String_converter_should_preserve_identity_child_length_without_requiring_the_parent()
    {
        using var factory = CreateFactory(restoreType: false);
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var schema = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("TestChildRulesNullability");
        schema.GetProperty("properties").GetProperty("country").GetProperty("minLength").GetInt32().Should().Be(1);
        schema.GetProperty("required").EnumerateArray().Select(x => x.GetString()).Should().NotContain("country");
    }

    private static WebApplicationFactory<TestMarker> CreateFactory(bool restoreType)
    {
        return new AspNetCoreOpenApiTests.TestWebApplicationFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new EmptyStringToNullConverter()));
                if (restoreType)
                {
                    // Match issue #238: the user's transformer runs AFTER FluentValidation.
                    services.Configure<OpenApiOptions>("v1", options => options.AddSchemaTransformer((schema, context, _) =>
                    {
                        if (context.JsonTypeInfo.Type == typeof(TestNullableLengthRequest) && schema.Properties is not null)
                        {
                            foreach (var property in schema.Properties.Values)
                            {
#if OPENAPI_V2
                                if (property is Microsoft.OpenApi.OpenApiSchema concrete)
                                    concrete.Type = Microsoft.OpenApi.JsonSchemaType.String | Microsoft.OpenApi.JsonSchemaType.Null;
#else
                                property.Type = "string";
                                property.Nullable = true;
#endif
                            }
                        }

                        return Task.CompletedTask;
                    }));
                }
            }));
    }

    private sealed class EmptyStringToNullConverter : JsonConverter<string?>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            return string.IsNullOrEmpty(value) ? null : value;
        }

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) => writer.WriteStringValue(value);
    }

    private static void ApplyLength<T>(InlineValidator<T> validator, OpenApiSchema property, IPropertyValidator propertyValidator)
    {
        var validationRule = validator.CreateDescriptor().Rules.Single();
        var schema = new OpenApiSchema();
#if OPENAPI_V2
        schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
#else
        schema.Properties ??= new Dictionary<string, OpenApiSchema>();
#endif
        schema.Properties.Add(validationRule.PropertyName, property);
        var validatorContext = new ValidatorContext(new TypeContext(typeof(T), new SchemaGenerationOptions()), validator);
        var ruleContext = new OpenApiRuleContext(schema, validationRule.PropertyName,
            new ValidationRuleContext(validatorContext, validationRule), propertyValidator);
        new DefaultFluentValidationRuleProvider().GetRules().Single(rule => rule.Name == "Length").Apply!(ruleContext);
    }

    private sealed record NonStringRequest(int Count, Dictionary<string, string>? Values);
}
