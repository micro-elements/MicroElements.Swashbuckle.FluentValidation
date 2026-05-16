using System.Linq;
using System.Text.RegularExpressions;
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
    /// https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/70
    /// </summary>
    public class MultipleMatchRules : UnitTestBase
    {
        private class PhoneEntity
        {
            public string MobilePhoneNumber { get; set; }

            public class Validator : AbstractValidator<PhoneEntity>
            {
                public Validator()
                {
                    RuleFor(c => c.MobilePhoneNumber)
                        .NotEmpty()
                        .Length(10)
                        .Matches(@"^3").WithMessage("'{PropertyName}' should start with '3'.")
                        .Matches(@"^\d*$").WithMessage("'{PropertyName}' should only contain digits.");
                }
            }
        }

        /// <summary>
        /// https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/70
        /// </summary>
        [Fact]
        public void multiple_match_rules()
        {
            // *************************
            // FluentValidation behavior
            // *************************

            void ShouldBeSuccess(PhoneEntity entity) => new PhoneEntity.Validator().ValidateAndThrow(entity);
            void ShouldBeFailed(PhoneEntity entity)
            {
                var validationResult = new PhoneEntity.Validator().Validate(entity);
                validationResult.IsValid.Should().BeFalse();
            }

            // 10 digits, starts with 3
            ShouldBeSuccess(new PhoneEntity() { MobilePhoneNumber = "3123456789" });
            // less then 10
            ShouldBeFailed(new PhoneEntity() { MobilePhoneNumber = "333" });
            // has symbols
            ShouldBeFailed(new PhoneEntity() { MobilePhoneNumber = "3a23456789" });
            // has symbols, not starts with 3
            ShouldBeFailed(new PhoneEntity() { MobilePhoneNumber = "1a23456789" });

            // *********************************
            // FluentValidation swagger behavior
            // *********************************

            var schema = new SchemaRepository().GenerateSchemaForValidator(new PhoneEntity.Validator());
            var numberProp = schema.GetProperty(nameof(PhoneEntity.MobilePhoneNumber))!;
            numberProp.GetTypeString().Should().Be("string");
        }

        private class CreateUserRequest
        {
            public string Password { get; set; }

            public class Validator : AbstractValidator<CreateUserRequest>
            {
                public Validator()
                {
                    RuleFor(x => x.Password)
                        .NotEmpty()
                        .MinimumLength(8)
                        .MaximumLength(256)
                        .Matches("[a-z]").WithMessage("'Password' must contain at least one lowercase letter.")
                        .Matches("[A-Z]").WithMessage("'Password' must contain at least one uppercase letter.")
                        .Matches("[0-9]").WithMessage("'Password' must contain at least one digit.");
                }
            }
        }

        /// <summary>
        /// By default multiple <c>.Matches()</c> rules are combined into a single 'pattern'.
        /// https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/204
        /// </summary>
        [Fact]
        public void multiple_match_rules_combined_into_single_pattern()
        {
            var schema = new SchemaRepository().GenerateSchemaForValidator(new CreateUserRequest.Validator());
            var passwordProp = schema.GetProperty(nameof(CreateUserRequest.Password))!;

            const string expected = "(?=[\\s\\S]*(?:[a-z]))(?=[\\s\\S]*(?:[A-Z]))(?=[\\s\\S]*(?:[0-9]))";

            passwordProp.Pattern.Should().Be(expected);
            (passwordProp.AllOf?.Count ?? 0).Should().Be(0, "patterns are merged into a single 'pattern'");

            // The combined pattern keeps the .Matches() semantics: all three constraints are required.
            Regex.IsMatch("Abcdef12", expected).Should().BeTrue();
            Regex.IsMatch("abcdefgh", expected).Should().BeFalse("missing uppercase and digit");
            Regex.IsMatch("ABCDEF12", expected).Should().BeFalse("missing lowercase");
        }

        /// <summary>
        /// Edge case: a user-supplied pattern that itself starts with a lookahead must still be
        /// wrapped when combined, so the combined patterns stay independent.
        /// </summary>
        [Fact]
        public void multiple_match_rules_combine_user_lookahead_pattern()
        {
            var validator = new InlineValidator<CreateUserRequest>();
            validator.RuleFor(x => x.Password)
                .Matches("(?=x)")
                .Matches("[0-9]");

            var schema = new SchemaRepository().GenerateSchemaForValidator(validator);
            var passwordProp = schema.GetProperty(nameof(CreateUserRequest.Password))!;

            // The first pattern is wrapped too — not appended as-is.
            passwordProp.Pattern.Should().Be("(?=[\\s\\S]*(?:(?=x)))(?=[\\s\\S]*(?:[0-9]))");
        }

        /// <summary>
        /// With <see cref="MicroElements.OpenApi.FluentValidation.SchemaGenerationOptions.UseAllOfForMultipleRules"/>
        /// the legacy 'allOf' representation is preserved.
        /// </summary>
        [Fact]
        public void multiple_match_rules_use_allOf_when_opted_in()
        {
            var schema = new SchemaRepository().GenerateSchemaForValidator(
                new CreateUserRequest.Validator(),
                configureSchemaGenerationOptions: options => options.UseAllOfForMultipleRules = true);
            var passwordProp = schema.GetProperty(nameof(CreateUserRequest.Password))!;

            passwordProp.Pattern.Should().BeNull();
            passwordProp.AllOf.Should().NotBeNull();
            passwordProp.AllOf!.Select(s => ((OpenApiSchema)s).Pattern)
                .Should().BeEquivalentTo("[a-z]", "[A-Z]", "[0-9]");
        }
    }
}