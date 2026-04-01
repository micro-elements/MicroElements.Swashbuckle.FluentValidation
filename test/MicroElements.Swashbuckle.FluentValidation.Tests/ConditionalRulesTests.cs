using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using MicroElements.OpenApi;
using MicroElements.OpenApi.FluentValidation;
using MicroElements.Swashbuckle.FluentValidation.Tests.Samples;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace MicroElements.Swashbuckle.FluentValidation.Tests;

/// <summary>
/// Tests for <see cref="ConditionalRulesMode"/> (Issue #203).
/// </summary>
public class ConditionalRulesTests
{
    /// <summary>
    /// Issue #203: ConditionalRulesMode.Exclude (default) should skip rules with .When().
    /// </summary>
    [Fact]
    public void ConditionalRulesMode_Exclude_Should_Skip_Conditional_Rules()
    {
        var schemaRepository = new SchemaRepository();
        var validator = new InlineValidator<Customer>();

        validator.RuleFor(x => x.Discount)
            .GreaterThan(0)
            .When(x => x.Id == 1);

        var schema = schemaRepository.GenerateSchemaForValidator(validator);

        schema.GetProperty("Discount")!.GetMinimum().Should().BeNull();
    }

    /// <summary>
    /// Issue #203: ConditionalRulesMode.Include should include rules with .When() in schema.
    /// </summary>
    [Fact]
    public void ConditionalRulesMode_Include_Should_Include_Conditional_Rules()
    {
        var schemaRepository = new SchemaRepository();
        var validator = new InlineValidator<Customer>();

        validator.RuleFor(x => x.Discount)
            .GreaterThan(0)
            .When(x => x.Id == 1);

        var schema = schemaRepository.GenerateSchemaForValidator(
            validator,
            configureSchemaGenerationOptions: options =>
            {
                options.ConditionalRules = ConditionalRulesMode.Include;
            });

        schema.GetProperty("Discount")!.GetMinimum().Should().Be(0);
    }

    /// <summary>
    /// Issue #203: ConditionalRulesMode.IncludeWithWarning should include conditional rules in schema.
    /// </summary>
    /// <remarks>
    /// TODO: This test only verifies the schema is correct. It does not assert that a warning is logged.
    /// To verify warning emission, inject a test ILoggerFactory (e.g., Microsoft.Extensions.Logging.Testing).
    /// </remarks>
    [Fact]
    public void ConditionalRulesMode_IncludeWithWarning_Should_Include_Conditional_Rules()
    {
        var schemaRepository = new SchemaRepository();
        var validator = new InlineValidator<Customer>();

        validator.RuleFor(x => x.Discount)
            .GreaterThan(0)
            .When(x => x.Id == 1);

        var schema = schemaRepository.GenerateSchemaForValidator(
            validator,
            configureSchemaGenerationOptions: options =>
            {
                options.ConditionalRules = ConditionalRulesMode.IncludeWithWarning;
            });

        schema.GetProperty("Discount")!.GetMinimum().Should().Be(0);
    }

    /// <summary>
    /// Issue #203: ConditionalRulesMode.IncludeWithWarning should include component-level conditional rules.
    /// </summary>
    [Fact]
    public void ConditionalRulesMode_IncludeWithWarning_Should_Include_Component_Level_Conditional_Rules()
    {
        var schemaRepository = new SchemaRepository();
        var validator = new InlineValidator<Customer>();

        validator.RuleFor(x => x.Surname)
            .NotEmpty()
            .WhenAsync((x, _) => Task.FromResult(x.Id == 1));

        var schema = schemaRepository.GenerateSchemaForValidator(
            validator,
            configureSchemaGenerationOptions: options =>
            {
                options.ConditionalRules = ConditionalRulesMode.IncludeWithWarning;
            });

        schema.GetProperty("Surname")!.MinLength.Should().Be(1);
    }

    /// <summary>
    /// Issue #203: ConditionalRulesMode should also work for component-level conditions (.WhenAsync on individual validator).
    /// </summary>
    [Fact]
    public void ConditionalRulesMode_Include_Should_Include_Component_Level_Conditional_Rules()
    {
        var schemaRepository = new SchemaRepository();
        var validator = new InlineValidator<Customer>();

        validator.RuleFor(x => x.Surname)
            .NotEmpty()
            .WhenAsync((x, _) => Task.FromResult(x.Id == 1));

        var schema = schemaRepository.GenerateSchemaForValidator(
            validator,
            configureSchemaGenerationOptions: options =>
            {
                options.ConditionalRules = ConditionalRulesMode.Include;
            });

        schema.GetProperty("Surname")!.MinLength.Should().Be(1);
    }
}
