using FluentAssertions;
using FluentValidation;
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
            var numberProp = schema.Properties[nameof(PhoneEntity.MobilePhoneNumber)];
            numberProp.Type.Should().Be("string");
        }
    }
}