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
    /// https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/68
    /// </summary>
    public class SetValidatorTest : UnitTestBase
    {
        public interface IEmail
        {
            string Email { get; set; }
        }

        public class RegisterUserRequest : IEmail
        {
            public string GivenName { get; set; }
            public string SurName { get; set; }
            public string Email { get; set; }
        }

        public class EmailValidator : AbstractValidator<IEmail>
        {
            public EmailValidator()
            {
                RuleFor(x => x.Email).NotEmpty().EmailAddress();
            }
        }

        public class RegisterUserRequestValidator : AbstractValidator<RegisterUserRequest>
        {
            public RegisterUserRequestValidator()
            {
                RuleFor(x => x).SetValidator(new EmailValidator());
                RuleFor(x => x.SurName).NotEmpty();
                RuleFor(x => x.GivenName).NotEmpty();
            }
        }

        [Fact]
        public void set_validator_should_be_applied()
        {
            // *********************************
            // FluentValidation swagger behavior
            // *********************************

            var schemaRepository = new SchemaRepository();
            var referenceSchema = SchemaGenerator(new RegisterUserRequestValidator()).GenerateSchema(typeof(RegisterUserRequest), schemaRepository);

            var schema = schemaRepository.GetSchema(referenceSchema.GetRefId()!);
            var emailProperty = schema.GetProperty(nameof(RegisterUserRequest.Email))!;
            emailProperty.GetTypeString().Should().Be("string");
            emailProperty.Format.Should().Be("email");
            emailProperty.MinLength.Should().Be(1);
        }
    }
}