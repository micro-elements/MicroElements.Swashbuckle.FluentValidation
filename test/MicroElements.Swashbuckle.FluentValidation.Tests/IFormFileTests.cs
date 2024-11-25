using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace MicroElements.Swashbuckle.FluentValidation.Tests
{
    public class IFormFileTests : UnitTestBase
    {
        public class UploadFileRequest
        {
            [FromForm(Name = "File")]
            public IFormFile File { get; set; }
        }

        public class UploadFileRequestValidator : AbstractValidator<UploadFileRequest>
        {
            public UploadFileRequestValidator()
            {
                RuleFor(c => c.File)
                    .Cascade(CascadeMode.Stop)
                    .NotEmpty();
            }
        }

        [Fact]
        public void all_rules_should_be_applied()
        {
            var schemaRepository = new SchemaRepository();
            var referenceSchema = SchemaGenerator(new UploadFileRequestValidator()).GenerateSchema(typeof(UploadFileRequest), schemaRepository);

            var schema = schemaRepository.Schemas[referenceSchema.Reference.Id];
            var fileProperty = schema.Properties[nameof(UploadFileRequest.File)];
            fileProperty.Type.Should().Be("string");
            fileProperty.Format.Should().Be("binary");
            fileProperty.Nullable.Should().Be(false);
        }
    }
}
