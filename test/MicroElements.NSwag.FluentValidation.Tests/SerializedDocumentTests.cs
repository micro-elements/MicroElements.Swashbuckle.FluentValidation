// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using FluentValidation;
using MicroElements.OpenApi.FluentValidation;
using Microsoft.Extensions.Options;
using NJsonSchema;
using NJsonSchema.Generation;
using Xunit;

namespace MicroElements.NSwag.FluentValidation.Tests;

/// <summary>
/// Issue #236 / ADR-008 Phase 3: narrow serialized-document assertion. The NSwag adapter targets
/// net8.0/net9.0 only (ADR-008 Decision 3); running this test on the net11.0 test target verifies
/// transitively that NSwag's net8.0 assets (NJsonSchema + our processor) keep working at runtime
/// on .NET 11, including document serialization — which the existing unit-level tests do not cover.
/// </summary>
public class SerializedDocumentTests
{
    public class Device
    {
        public string SerialNumber { get; set; } = string.Empty;
    }

    [Fact]
    public void Serialized_document_keeps_validation_constraints()
    {
        var validator = new InlineValidator<Device>();
        validator.RuleFor(x => x.SerialNumber).NotEmpty().MinimumLength(3).MaximumLength(24);

        var options = new SchemaGenerationOptions { SchemaIdSelector = type => type.Name }.FillDefaultValues(null);
        var settings = new SystemTextJsonSchemaGeneratorSettings();
        settings.SchemaProcessors.Add(new FluentValidationSchemaProcessor(
            validatorFactory: new ValidatorRegistry(new[] { validator }, Options.Create(options)),
            schemaGenerationOptions: Options.Create(options)));

        var schema = JsonSchema.FromType<Device>(settings);
        var json = schema.ToJson();

        json.Should().Contain("\"minLength\": 3");
        json.Should().Contain("\"maxLength\": 24");
        json.Should().Contain("\"required\"");
    }
}
