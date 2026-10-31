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

public class ChildRulesNullabilityTests
{
    public class Request
    {
        public string? Country { get; set; }
        public string[]? Countries { get; set; }
        public string? RequiredCountry { get; set; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Identity_child_rules_should_preserve_nullable_properties_and_length_constraints(bool notEmpty)
    {
        var validator = new InlineValidator<Request>();
        validator.RuleFor(x => x.Country).ChildRules(child =>
        {
            if (notEmpty) child.RuleFor(x => x).NotEmpty();
            else child.RuleFor(x => x).NotNull();
            child.RuleFor(x => x).Matches("^[A-Z]+$");
        });
        validator.RuleFor(x => x.Countries).ChildRules(child => child.RuleFor(x => x).NotEmpty());
        validator.RuleFor(x => x.RequiredCountry).NotNull().ChildRules(child => child.RuleFor(x => x).NotEmpty());

        validator.Validate(new Request { RequiredCountry = "GB" }).IsValid.Should().BeTrue();
        var options = new SchemaGenerationOptions { SchemaIdSelector = type => type.Name }.FillDefaultValues(null);
        var settings = new SystemTextJsonSchemaGeneratorSettings();
        settings.SchemaProcessors.Add(new FluentValidationSchemaProcessor(
            validatorFactory: new ValidatorRegistry(new[] { validator }, Options.Create(options)),
            schemaGenerationOptions: Options.Create(options)));
        var schema = JsonSchema.FromType<Request>(settings);

        schema.RequiredProperties.Should().NotContain(nameof(Request.Country));
        schema.RequiredProperties.Should().NotContain(nameof(Request.Countries));
        schema.RequiredProperties.Should().Contain(nameof(Request.RequiredCountry));
        var country = schema.Properties[nameof(Request.Country)];
        country.IsNullable(settings.SchemaType).Should().BeTrue();
        country.MinLength.Should().Be(notEmpty ? 1 : null);
        country.Pattern.Should().Be("^[A-Z]+$");
        var countries = schema.Properties[nameof(Request.Countries)];
        countries.IsNullable(settings.SchemaType).Should().BeTrue();
        countries.MinItems.Should().Be(1);
        countries.MinLength.Should().BeNull();
        schema.Properties[nameof(Request.RequiredCountry)].IsNullable(settings.SchemaType).Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Child_not_empty_should_preserve_stronger_parent_length(bool childFirst)
    {
        var validator = new InlineValidator<Request>();
        var parent = validator.RuleFor(x => x.Country);
        if (childFirst)
            parent.ChildRules(child => child.RuleFor(x => x).NotEmpty()).MinimumLength(5);
        else
            parent.MinimumLength(5).ChildRules(child => child.RuleFor(x => x).NotEmpty());
        var options = new SchemaGenerationOptions { SchemaIdSelector = type => type.Name }.FillDefaultValues(null);
        var settings = new SystemTextJsonSchemaGeneratorSettings();
        settings.SchemaProcessors.Add(new FluentValidationSchemaProcessor(
            validatorFactory: new ValidatorRegistry(new[] { validator }, Options.Create(options)),
            schemaGenerationOptions: Options.Create(options)));
        var schema = JsonSchema.FromType<Request>(settings);
        schema.Properties[nameof(Request.Country)].MinLength.Should().Be(5);
        schema.RequiredProperties.Should().NotContain(nameof(Request.Country));
    }

}
