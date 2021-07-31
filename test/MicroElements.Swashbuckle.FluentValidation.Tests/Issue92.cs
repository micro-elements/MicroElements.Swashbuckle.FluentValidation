using FluentAssertions;
using FluentValidation;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace MicroElements.Swashbuckle.FluentValidation.Tests
{
    /// <summary>
    /// https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/92
    /// </summary>
    public class Issue92 : UnitTestBase
    {
        // Command object to send in a POST call.
        public class CreateBookshelfCommand
        {
            public string[] Books { get; set; }
        }

        // Validator to validate that command.
        public class CreateBookshelfCommandValidator : AbstractValidator<CreateBookshelfCommand>
        {
            public CreateBookshelfCommandValidator()
            {
                RuleFor(c => c.Books)
                    .NotEmpty();

                RuleForEach(c => c.Books)
                    .NotEmpty()
                    .MinimumLength(5)
                    .MaximumLength(250);
            }
        }

        [Fact]
        public void all_rules_should_be_applied()
        {
            var schemaRepository = new SchemaRepository();
            var referenceSchema = SchemaGenerator(new CreateBookshelfCommandValidator()).GenerateSchema(typeof(CreateBookshelfCommand), schemaRepository);

            var schema = schemaRepository.Schemas[referenceSchema.Reference.Id];
            var booksProperty = schema.Properties[nameof(CreateBookshelfCommand.Books)];
            booksProperty.Type.Should().Be("array");

            // should use MinItems for array
            booksProperty.MinItems.Should().Be(1);
            booksProperty.MinLength.Should().Be(null);

            // items validation should be set
            booksProperty.Items.Type.Should().Be("string");
            booksProperty.Items.MinLength.Should().Be(5);
            booksProperty.Items.MaxLength.Should().Be(250);
        }
    }
}