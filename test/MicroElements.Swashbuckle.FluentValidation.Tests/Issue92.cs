using FluentAssertions;
using FluentValidation;
#if OPENAPI_V2
using Microsoft.OpenApi;
#else
using Microsoft.OpenApi.Models;
#endif
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

            var schema = schemaRepository.GetSchema(referenceSchema.GetRefId()!);
            var booksProperty = schema.GetProperty(nameof(CreateBookshelfCommand.Books))!;
            booksProperty.GetTypeString().Should().Be("array");

            // should use MinItems for array
            booksProperty.MinItems.Should().Be(1);
            booksProperty.MinLength.Should().Be(null);

            // items validation should be set
            var items = booksProperty.GetItems()!;
            items.GetTypeString().Should().Be("string");
            items.MinLength.Should().Be(5);
            items.MaxLength.Should().Be(250);
        }
    }
}