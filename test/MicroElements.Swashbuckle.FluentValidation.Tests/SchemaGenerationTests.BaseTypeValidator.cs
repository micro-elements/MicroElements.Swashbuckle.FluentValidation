using FluentAssertions;
using FluentValidation;
using MicroElements.OpenApi.FluentValidation;
using Xunit;

namespace MicroElements.Swashbuckle.FluentValidation.Tests;

public partial class SchemaGenerationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BaseTypeValidator(bool searchBaseTypeValidators)
    {
        new SwaggerTestHost()
            .Configure(options => options.ValidatorSearch = new ValidatorSearchSettings{SearchBaseTypeValidators = searchBaseTypeValidators})
            .RegisterValidator<AbstractInstitutionModelValidator>()
            .GenerateSchema<AbstractInstitutionModel>(out var schemaBase)
            .GenerateSchema<InstitutionModel>(out var schemaChild);

        if (searchBaseTypeValidators)
        {
            schemaBase.Properties["Name"].MaxLength.Should().Be(100);
            schemaChild.Properties["Name"].MaxLength.Should().Be(100);
        }
        else
        {
            schemaBase.Properties["Name"].MaxLength.Should().Be(100);
            schemaChild.Properties["Name"].MaxLength.Should().Be(null, because: "Validator is for base type and no validator was for concrete type");
        }
    }

    // abstract
    public abstract class AbstractInstitutionModel
    {
        public string Name { get; set; }
    }

    // class
    public class InstitutionModel : AbstractInstitutionModel
    {
    }

    // fluent validator
    public class AbstractInstitutionModelValidator : AbstractValidator<AbstractInstitutionModel>
    {
        public AbstractInstitutionModelValidator()
        {
            RuleFor(p => p.Name)
                .NotEmpty()
                .MaximumLength(100);
        }
    }
}