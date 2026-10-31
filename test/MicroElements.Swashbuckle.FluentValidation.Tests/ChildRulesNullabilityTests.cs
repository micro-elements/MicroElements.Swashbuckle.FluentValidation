// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using FluentAssertions;
using FluentValidation;
using MicroElements.OpenApi;
using MicroElements.OpenApi.Core;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace MicroElements.Swashbuckle.FluentValidation.Tests;

public class ChildRulesNullabilityTests
{
    public class Request
    {
        public string? Country { get; set; }
        public string?[]? Countries { get; set; }
        public int Count { get; set; }
        public Dictionary<string, string>? Values { get; set; }
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void Child_presence_rules_should_preserve_parent_nullability(bool useSetValidator, bool notEmpty, bool required)
    {
        var validator = new InlineValidator<Request>();
        var parent = validator.RuleFor(x => x.Country);
        if (required)
            parent.NotNull();

        if (useSetValidator)
        {
            var child = new InlineValidator<string?>();
            if (notEmpty) child.RuleFor(x => x).NotEmpty();
            else child.RuleFor(x => x).NotNull();
            parent.SetValidator(child);
        }
        else
        {
            parent.ChildRules(child =>
            {
                if (notEmpty) child.RuleFor(x => x).NotEmpty();
                else child.RuleFor(x => x).NotNull();
            });
        }

        validator.Validate(new Request()).IsValid.Should().Be(!required);
        validator.Validate(new Request { Country = "" }).IsValid.Should().Be(!notEmpty);
        var schema = new SchemaRepository().GenerateSchemaForValidator(validator);
        schema.Required.Contains(nameof(Request.Country)).Should().Be(required);
        var country = schema.GetProperty(nameof(Request.Country))!;
        country.IsNullable().Should().Be(!required);
        country.MinLength.Should().Be(notEmpty ? 1 : null);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Child_not_empty_should_keep_min_items_without_requiring_the_array(bool required)
    {
        var validator = new InlineValidator<Request>();
        var parent = validator.RuleFor(x => x.Countries);
        if (required)
            parent.NotEmpty();
        parent.ChildRules(child => child.RuleFor(x => x).NotEmpty());

        validator.Validate(new Request()).IsValid.Should().Be(!required);
        validator.Validate(new Request { Countries = [] }).IsValid.Should().BeFalse();
        var schema = new SchemaRepository().GenerateSchemaForValidator(validator);
        schema.Required.Contains(nameof(Request.Countries)).Should().Be(required);
        var countries = schema.GetProperty(nameof(Request.Countries))!;
        countries.IsNullable().Should().Be(!required);
        countries.MinItems.Should().Be(1);
        countries.MinLength.Should().BeNull();
    }

    [Fact]
    public void Child_not_empty_on_items_should_preserve_item_nullability_and_optional_array()
    {
        var validator = new InlineValidator<Request>();
        validator.RuleForEach(x => x.Countries).ChildRules(child => child.RuleFor(x => x).NotEmpty());
        validator.Validate(new Request { Countries = [null] }).IsValid.Should().BeTrue();
        validator.Validate(new Request { Countries = [""] }).IsValid.Should().BeFalse();
        var schema = new SchemaRepository().GenerateSchemaForValidator(validator);
        schema.Required.Should().NotContain(nameof(Request.Countries));
        var countries = schema.GetProperty(nameof(Request.Countries))!;
        countries.MinItems.Should().BeNull();
        countries.Items!.MinLength.Should().Be(1);
        // Item nullability is supplied by the schema generator; child rules must not tighten it.
        var baseline = new SchemaRepository().GenerateSchemaForValidator(new InlineValidator<Request>());
        var baselineItems = OpenApiSchemaCompatibility.GetItems(baseline.GetProperty(nameof(Request.Countries))!)!;
        OpenApiSchemaCompatibility.GetItems(countries)!.IsNullable().Should().Be(baselineItems.IsNullable());
    }

    [Fact]
    public void Nested_identity_presence_rules_should_not_require_the_parent()
    {
        var validator = new InlineValidator<Request>();
        validator.RuleFor(x => x.Country).ChildRules(child =>
            child.RuleFor(x => x).ChildRules(nested => nested.RuleFor(x => x).NotEmpty().Matches("^[A-Z]+$")));
        var schema = new SchemaRepository().GenerateSchemaForValidator(validator);
        schema.Required.Should().NotContain(nameof(Request.Country));
        var country = schema.GetProperty(nameof(Request.Country))!;
        country.IsNullable().Should().BeTrue();
        country.MinLength.Should().Be(1);
        country.Pattern.Should().Be("^[A-Z]+$");
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
        var schema = new SchemaRepository().GenerateSchemaForValidator(validator);
        schema.GetProperty(nameof(Request.Country))!.MinLength.Should().Be(5);
        schema.Required.Should().NotContain(nameof(Request.Country));
    }

    [Fact]
    public void Child_not_empty_on_non_length_types_should_not_emit_length_constraints()
    {
        var validator = new InlineValidator<Request>();
        validator.RuleFor(x => x.Count).ChildRules(child => child.RuleFor(x => x).NotEmpty());
        validator.RuleFor(x => x.Values).ChildRules(child => child.RuleFor(x => x).NotEmpty());
        var schema = new SchemaRepository().GenerateSchemaForValidator(validator);
        schema.Required.Should().NotContain(nameof(Request.Values));
        schema.GetProperty(nameof(Request.Count))!.MinLength.Should().BeNull();
        schema.GetProperty(nameof(Request.Values))!.MinLength.Should().BeNull();
    }
}
