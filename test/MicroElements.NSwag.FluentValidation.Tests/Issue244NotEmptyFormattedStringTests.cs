// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using FluentAssertions;
using FluentValidation;
using MicroElements.OpenApi.FluentValidation;
using Microsoft.Extensions.Options;
using NJsonSchema;
using NJsonSchema.Generation;
using Xunit;

namespace MicroElements.NSwag.FluentValidation.Tests;

/// <summary>
/// NotEmpty on a value type serialized as a formatted string (Guid, DateTime, ...) must not emit minLength: 1.
/// For those types NotEmpty means "not the default value", which JSON Schema cannot express.
/// A plain string keeps minLength: 1.
/// </summary>
public class Issue244NotEmptyFormattedStringTests
{
    public class Request
    {
        public Guid PersonId { get; set; }
        public Guid? OptionalPersonId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public TimeSpan Duration { get; set; }
        [NJsonSchema.Annotations.JsonSchema(JsonObjectType.String, Format = "uuid")]
        public string ExternalId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }

    [Theory]
    [InlineData(nameof(Request.PersonId), "guid")]
    [InlineData(nameof(Request.OptionalPersonId), "guid")]
    [InlineData(nameof(Request.CreatedAt), "date-time")]
    [InlineData(nameof(Request.UpdatedAt), "date-time")]
    [InlineData(nameof(Request.Duration), "duration")]
    public void NotEmpty_on_formatted_value_type_should_not_emit_min_length(string propertyName, string format)
    {
        var schema = GenerateSchema();

        var property = schema.Properties[propertyName];
        property.Format.Should().Be(format);
        property.MinLength.Should().BeNull();
        schema.RequiredProperties.Should().Contain(propertyName);
    }

    [Fact]
    public void NotEmpty_on_string_should_emit_min_length()
    {
        var schema = GenerateSchema();

        schema.Properties[nameof(Request.Name)].MinLength.Should().Be(1);
        var externalId = schema.Properties[nameof(Request.ExternalId)];
        externalId.Format.Should().Be("uuid");
        externalId.MinLength.Should().Be(1);
    }

    private static JsonSchema GenerateSchema()
    {
        var validator = new InlineValidator<Request>();
        validator.RuleFor(x => x.PersonId).NotEmpty();
        validator.RuleFor(x => x.OptionalPersonId).NotEmpty();
        validator.RuleFor(x => x.CreatedAt).NotEmpty();
        validator.RuleFor(x => x.UpdatedAt).NotEmpty();
        validator.RuleFor(x => x.Duration).NotEmpty();
        validator.RuleFor(x => x.Name).NotEmpty();
        validator.RuleFor(x => x.ExternalId).NotEmpty();

        var options = new SchemaGenerationOptions { SchemaIdSelector = type => type.Name }.FillDefaultValues(null);
        var settings = new SystemTextJsonSchemaGeneratorSettings();
        settings.SchemaProcessors.Add(new FluentValidationSchemaProcessor(
            validatorFactory: new ValidatorRegistry(new[] { validator }, Options.Create(options)),
            schemaGenerationOptions: Options.Create(options)));
        return JsonSchema.FromType<Request>(settings);
    }
}
