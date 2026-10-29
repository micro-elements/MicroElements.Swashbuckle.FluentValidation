using System;
using FluentAssertions;
using FluentValidation;
using MicroElements.OpenApi.Core;
using MicroElements.OpenApi.FluentValidation;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace MicroElements.Swashbuckle.FluentValidation.Tests;

public class ChildRulesTests
{
    internal const string CountryPattern = "^[A-Z]{2}$";

    public class Request
    {
        public string Country { get; set; } = null!;

        public string Other { get; set; } = null!;

        public string[] Countries { get; set; } = Array.Empty<string>();
    }

    [Fact]
    public void Identity_child_rules_should_apply_to_the_parent_property()
    {
        var calls = 0;
        var validator = new InlineValidator<Request>();
        validator.RuleFor(x => x.Country)
            .NotEmpty()
            .ValidCountry(_ =>
            {
                calls++;
                return false;
            })
            .MaximumLength(3);

        var schema = new SchemaRepository().GenerateSchemaForValidator(validator);

        var country = schema.GetProperty(nameof(Request.Country))!;
        country.Pattern.Should().Be(CountryPattern);
        country.MinLength.Should().Be(1);
        country.MaxLength.Should().Be(3);
        schema.GetProperty(nameof(Request.Other))!.Pattern.Should().BeNull();
        calls.Should().Be(0);
    }

    [Fact]
    public void Unresolvable_child_validators_should_not_discard_parent_constraints()
    {
        Func<Request, string, IValidator<string>> provider = (_, _) =>
            throw new InvalidOperationException("A runtime model is required.");
        var validator = new InlineValidator<Request>();
        validator.RuleFor(x => x.Country).NotEmpty().MaximumLength(3).SetValidator(provider);

        var schema = new SchemaRepository().GenerateSchemaForValidator(validator);

        var country = schema.GetProperty(nameof(Request.Country))!;
        country.MinLength.Should().Be(1);
        country.MaxLength.Should().Be(3);
    }

    [Fact]
    public void Shared_identity_validators_should_apply_to_each_owning_property()
    {
        var child = new InlineValidator<string>();
        child.RuleFor(x => x).Matches(CountryPattern);
        var validator = new InlineValidator<Request>();
        validator.RuleFor(x => x.Country).SetValidator(child);
        validator.RuleFor(x => x.Other).SetValidator(child);

        var schema = new SchemaRepository().GenerateSchemaForValidator(validator);

        schema.GetProperty(nameof(Request.Country))!.Pattern.Should().Be(CountryPattern);
        schema.GetProperty(nameof(Request.Other))!.Pattern.Should().Be(CountryPattern);
    }

    [Theory]
    [InlineData("invalid", false, 0)]
    [InlineData("GB", true, 1)]
    [InlineData(null, true, 0)]
    public void Dependent_predicate_should_only_run_after_the_pattern_passes(string? country, bool valid, int expectedCalls)
    {
        var calls = 0;
        var validator = new InlineValidator<Request>();
        validator.RuleLevelCascadeMode = CascadeMode.Continue;
        validator.RuleFor(x => x.Country).ValidCountry(_ =>
        {
            calls++;
            return true;
        });

        var result = validator.Validate(new Request { Country = country! });

        result.IsValid.Should().Be(valid);
        calls.Should().Be(expectedCalls);
    }

    [Theory]
    [InlineData(ConditionalRulesMode.Exclude, false)]
    [InlineData(ConditionalRulesMode.Include, true)]
    [InlineData(ConditionalRulesMode.IncludeWithWarning, true)]
    public void Identity_child_rules_should_respect_conditions(ConditionalRulesMode mode, bool include)
    {
        var validator = new InlineValidator<Request>();
        validator.RuleFor(x => x.Country).ChildRules(child =>
            child.RuleFor(x => x).Matches(CountryPattern).When(_ => false));

        var schema = new SchemaRepository().GenerateSchemaForValidator(
            validator,
            configureSchemaGenerationOptions: options => options.ConditionalRules = mode);

        schema.GetProperty(nameof(Request.Country))!.Pattern.Should().Be(include ? CountryPattern : null);
    }

    [Fact]
    public void Identity_child_rules_should_respect_custom_rule_filters()
    {
        var validator = new InlineValidator<Request>();
        validator.RuleFor(x => x.Country).ChildRules(child => child.RuleFor(x => x).Matches(CountryPattern));

        var schema = new SchemaRepository().GenerateSchemaForValidator(
            validator,
            configureSchemaGenerationOptions: options => options.RuleFilter =
                new Condition<ValidationRuleContext>(context => context.ValidationRule.Member != null));

        schema.GetProperty(nameof(Request.Country))!.Pattern.Should().BeNull();
    }

    [Fact]
    public void Nested_identity_child_rules_should_apply_to_the_parent_property()
    {
        var validator = new InlineValidator<Request>();
        validator.RuleFor(x => x.Country).ChildRules(child =>
            child.RuleFor(x => x).ChildRules(nested => nested.RuleFor(x => x).Matches(CountryPattern)));

        var schema = new SchemaRepository().GenerateSchemaForValidator(validator);

        schema.GetProperty(nameof(Request.Country))!.Pattern.Should().Be(CountryPattern);
    }

    [Fact]
    public void Child_member_rules_should_not_apply_to_the_parent_property()
    {
        var validator = new InlineValidator<Request>();
        validator.RuleFor(x => x.Country).ChildRules(child => child.RuleFor(x => x.Length).GreaterThan(5));

        var schema = new SchemaRepository().GenerateSchemaForValidator(validator);

        schema.GetProperty(nameof(Request.Country))!.GetMinimum().Should().BeNull();
    }

    [Fact]
    public void Identity_child_rules_on_collection_elements_should_apply_to_items()
    {
        var validator = new InlineValidator<Request>();
        validator.RuleForEach(x => x.Countries).ChildRules(child => child.RuleFor(x => x).Matches(CountryPattern));

        var schema = new SchemaRepository().GenerateSchemaForValidator(validator);

        var countries = schema.GetProperty(nameof(Request.Countries))!;
        countries.Pattern.Should().BeNull();
        countries.Items!.Pattern.Should().Be(CountryPattern);
    }
}

internal static class CountryRuleExtensions
{
    internal static IRuleBuilderOptions<T, string> ValidCountry<T>(
        this IRuleBuilder<T, string> ruleBuilder,
        Func<string, bool> exists)
    {
        return ruleBuilder.ChildRules(child =>
            child.RuleFor(x => x).Matches(ChildRulesTests.CountryPattern).DependentRules(() =>
                child.RuleFor(x => x).Must(exists).WithMessage("The country doesn't exist")));
    }
}
