using System.Collections.Generic;
using FluentAssertions;
using FluentValidation;
using Microsoft.OpenApi.Models;
using SampleWebApi.Contracts;
using SampleWebApi.Validators;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace MicroElements.Swashbuckle.FluentValidation.Tests
{
    public class SchemaGenerationTests : UnitTestBase
    {
        public class ComplexObject
        {
            public string TextProperty1 { get; set; }
            public string TextProperty2 { get; set; }
            public string TextProperty3 { get; set; }
        }

        public string TextProperty1 = nameof(ComplexObject.TextProperty1);
        public string TextProperty2 = nameof(ComplexObject.TextProperty2);
        public string TextProperty3 = nameof(ComplexObject.TextProperty3);

        public class ComplexObjectValidator : AbstractValidator<ComplexObject>
        {
            public ComplexObjectValidator()
            {
                RuleFor(x => x.TextProperty1).NotEmpty();
            }
        }

        [Fact]
        public void NotEmpty_Should_Set_MinLength()
        {
            var schemaRepository = new SchemaRepository();
            var referenceSchema = SchemaGenerator(new ComplexObjectValidator()).GenerateSchema(typeof(ComplexObject), schemaRepository);

            referenceSchema.Reference.Should().NotBeNull();
            referenceSchema.Reference.Id.Should().Be("ComplexObject");

            var schema = schemaRepository.Schemas[referenceSchema.Reference.Id];

            Assert.Equal("object", schema.Type);
            schema.Properties.Keys.Should().BeEquivalentTo(TextProperty1, TextProperty2, TextProperty3);

            schema.Properties[TextProperty1].MinLength.Should().Be(1);
        }

        public class Validator2 : AbstractValidator<ComplexObject>
        {
            public Validator2()
            {
                RuleFor(x => x.TextProperty1).NotEmpty().MaximumLength(64);
                RuleFor(x => x.TextProperty2).MaximumLength(64).NotEmpty();
                RuleFor(x => x.TextProperty3).NotNull().MaximumLength(64);
            }
        }

        [Fact]
        public void MaximumLength_ShouldNot_Override_NotEmpty()
        {
            var schemaRepository = new SchemaRepository();
            var referenceSchema = SchemaGenerator(new Validator2()).GenerateSchema(typeof(ComplexObject), schemaRepository);

            var schema = schemaRepository.Schemas[referenceSchema.Reference.Id];
    
            schema.Properties[TextProperty1].MinLength.Should().Be(1);
            schema.Properties[TextProperty1].MaxLength.Should().Be(64);
            schema.Properties[TextProperty1].Nullable.Should().BeFalse();

            schema.Properties[TextProperty2].MinLength.Should().Be(1);
            schema.Properties[TextProperty2].MaxLength.Should().Be(64);
            schema.Properties[TextProperty2].Nullable.Should().BeFalse();
        }

        /// <summary>
        /// https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/pull/67
        /// </summary>
        [Fact]
        public void MaximumLength_ShouldNot_Override_NotNull()
        {
            var schemaRepository = new SchemaRepository();
            var referenceSchema = SchemaGenerator(new Validator2()).GenerateSchema(typeof(ComplexObject), schemaRepository);

            var schema = schemaRepository.Schemas[referenceSchema.Reference.Id];

            schema.Properties[TextProperty3].MaxLength.Should().Be(64);
            schema.Properties[TextProperty3].Nullable.Should().BeFalse();
        }

        [Fact]
        public void SampleValidator_FromSampleApi_Test()
        {
            var schemaRepository = new SchemaRepository();
            var referenceSchema = SchemaGenerator(new SampleValidator()).GenerateSchema(typeof(Sample), schemaRepository);
            var schema = schemaRepository.Schemas[referenceSchema.Reference.Id];

            schema.Type.Should().Be("object");
            schema.Properties.Keys.Count.Should().Be(13);


            schema.Properties["NotNull"].Nullable.Should().BeFalse();
            schema.Required.Should().Contain("NotNull");

            schema.Properties["NotEmpty"].MinLength.Should().Be(1);

            schema.Properties["EmailAddressRegex"].Pattern.Should().NotBeNullOrEmpty();
            schema.Properties["EmailAddressRegex"].Format.Should().Be("email");
            schema.Properties["EmailAddress"].Format.Should().Be("email");

            schema.Properties["RegexField"].Pattern.Should().Be(@"(\d{4})-(\d{2})-(\d{2})");

            schema.Properties["ValueInRange"].Minimum.Should().Be(5);
            schema.Properties["ValueInRange"].ExclusiveMinimum.Should().BeNull();
            schema.Properties["ValueInRange"].Maximum.Should().Be(10);
            schema.Properties["ValueInRange"].ExclusiveMaximum.Should().BeNull();

            schema.Properties["ValueInRangeExclusive"].Minimum.Should().Be(5);
            schema.Properties["ValueInRangeExclusive"].ExclusiveMinimum.Should().BeTrue();
            schema.Properties["ValueInRangeExclusive"].Maximum.Should().Be(10);
            schema.Properties["ValueInRangeExclusive"].ExclusiveMaximum.Should().BeTrue();

            schema.Properties["ValueInRangeFloat"].Minimum.Should().Be((decimal)5.1f);
            schema.Properties["ValueInRangeFloat"].ExclusiveMinimum.Should().BeNull();
            schema.Properties["ValueInRangeFloat"].Maximum.Should().Be((decimal)10.2f);
            schema.Properties["ValueInRangeFloat"].ExclusiveMaximum.Should().BeNull();

            schema.Properties["ValueInRangeDouble"].Minimum.Should().Be((decimal)5.1d);
            schema.Properties["ValueInRangeDouble"].ExclusiveMinimum.Should().BeTrue();
            schema.Properties["ValueInRangeDouble"].Maximum.Should().Be((decimal)10.2d);
            schema.Properties["ValueInRangeDouble"].ExclusiveMaximum.Should().BeTrue();

            schema.Properties["DecimalValue"].Minimum.Should().Be(1.333m);
            schema.Properties["DecimalValue"].ExclusiveMinimum.Should().BeNull();
            schema.Properties["DecimalValue"].Maximum.Should().Be(200.333m);
            schema.Properties["DecimalValue"].ExclusiveMaximum.Should().BeNull();

            schema.Properties["NotEmptyWithMaxLength"].MinLength.Should().Be(1);
            schema.Properties["NotEmptyWithMaxLength"].MaxLength.Should().Be(50);
        }

        [Fact]
        public void CustomerValidator_FromSampleApi_Test()
        {
            var schemaRepository = new SchemaRepository();
            var referenceSchema = SchemaGenerator(new CustomerValidator()).GenerateSchema(typeof(Customer), schemaRepository);
            var schema = schemaRepository.Schemas[referenceSchema.Reference.Id];
            schema.Properties["Surname"].MinLength.Should().Be(1);
            schema.Properties["Forename"].MinLength.Should().Be(1);

            // From included validator
            schema.Properties["Address"].MinLength.Should().Be(20);
            schema.Properties["Address"].MaxLength.Should().Be(250);

            schema.Properties["Discount"].Should().BeEquivalentTo(new OpenApiSchema()
            {
                Type = "number",
                Format = "double",
                Minimum = 4,
                ExclusiveMinimum = true,
                Maximum = 5,
                ExclusiveMaximum = true
            });

            schema.Properties.Keys.Count.Should().Be(5);
        }

        [Theory]
        [InlineData(1, 2, 1)]
        [InlineData(2, 1, 1)]
        [InlineData(1, null, 1)]
        [InlineData(null, 1, 1)]
        public static void TestMaxOverride(int? first, int? second, int expected)
        {
            OpenApiSchema schemaProperty = new OpenApiSchema();

            schemaProperty.SetNewMax(p => p.MaxLength, first);
            schemaProperty.SetNewMax(p => p.MaxLength, second);

            schemaProperty.MaxLength.Should().Be(expected);
        }

        [Theory]
        [InlineData(1, 2, 2)]
        [InlineData(2, 1, 2)]
        [InlineData(1, null, 1)]
        [InlineData(null, 1, 1)]
        public static void TestMinOverride(int? first, int? second, int expected)
        {
            OpenApiSchema schemaProperty = new OpenApiSchema();

            schemaProperty.SetNewMin(p => p.MinLength, first);
            schemaProperty.SetNewMin(p => p.MinLength, second);

            schemaProperty.MinLength.Should().Be(expected);
        }

        public class Person
        {
            public List<string> Emails { get; set; } = new List<string>();
        }

        public class PersonValidator : AbstractValidator<Person>
        {
            public PersonValidator()
            {
                RuleForEach(x => x.Emails).EmailAddress();
            }
        }

        /// <summary>
        /// RuleForEach.
        /// https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/66
        /// </summary>
        [Fact]
        public void CollectionValidation()
        {
            var schemaRepository = new SchemaRepository();
            var referenceSchema = SchemaGenerator(new PersonValidator()).GenerateSchema(typeof(Person), schemaRepository);

            var schema = schemaRepository.Schemas[referenceSchema.Reference.Id];
            var emailsProp = schema.Properties[nameof(Person.Emails)];

            emailsProp.Format.Should().Be(null);

            emailsProp.Items.Type.Should().Be("string");
            emailsProp.Items.Format.Should().Be("email");
        }

        public class NumberEntity
        {
            public int Number { get; set; }
            public int? NullableNumber { get; set; }

            public class Validator : AbstractValidator<NumberEntity>
            {
                public Validator()
                {
                    RuleFor(c => c.Number).GreaterThan(0);
                    RuleFor(c => c.NullableNumber).GreaterThan(0);
                }
            }
        }

        /// <summary>
        /// https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/70
        /// </summary>
        [Fact]
        public void integer_property_should_not_be_nullable()
        {
            // *************************
            // FluentValidation behavior
            // *************************

            void ShouldBeSuccess(NumberEntity entity) => new NumberEntity.Validator().ValidateAndThrow(entity);
            void ShouldBeFailed(NumberEntity entity) => new NumberEntity.Validator().Validate(entity).IsValid.Should().BeFalse();

            ShouldBeSuccess(new NumberEntity() { Number = 1 });
            ShouldBeFailed(new NumberEntity() { Number = 0 });

            ShouldBeSuccess(new NumberEntity() { Number = 1, NullableNumber = 1 });
            ShouldBeFailed(new NumberEntity() { Number = 1, NullableNumber = 0 });
            // null is also valid
            ShouldBeSuccess(new NumberEntity() { Number = 1, NullableNumber = null });

            // *********************************
            // FluentValidation swagger behavior
            // *********************************

            var schemaRepository = new SchemaRepository();
            var referenceSchema = SchemaGenerator(new NumberEntity.Validator()).GenerateSchema(typeof(NumberEntity), schemaRepository);

            var schema = schemaRepository.Schemas[referenceSchema.Reference.Id];

            var numberProp = schema.Properties[nameof(NumberEntity.Number)];
            numberProp.Type.Should().Be("integer");
            numberProp.Nullable.Should().Be(false);
            numberProp.Minimum.Should().Be(0);
            numberProp.ExclusiveMinimum.Should().Be(true);

            var nullableNumberProp = schema.Properties[nameof(NumberEntity.NullableNumber)];
            nullableNumberProp.Type.Should().Be("integer");
            nullableNumberProp.Nullable.Should().Be(true);
            nullableNumberProp.Minimum.Should().Be(0);
            nullableNumberProp.ExclusiveMinimum.Should().Be(true);
        }


        public class TestEntity
        {
            public string TextValue { get; set; }

            public string? NullableTextValue { get; set; }
        }

        [Fact]
        public void TextNullability()
        {
            new SchemaBuilder<TestEntity>()
                .AddRule(entity => entity.TextValue,
                    rule => rule.MaximumLength(5),
                    schema => schema.Nullable.Should().Be(true));

            new SchemaBuilder<TestEntity>()
                .AddRule(entity => entity.NullableTextValue,
                    rule => rule.MaximumLength(5),
                    schema => schema.Nullable.Should().Be(true));
        }

        [Fact]
        public void NotNull()
        {
            var property = new SchemaBuilder<TestEntity>()
                .AddRule(entity => entity.TextValue, rule => rule.NotNull().MinimumLength(1));

            property.Nullable.Should().Be(false);
            property.MinLength.Should().Be(1);
        }

        [Fact]
        public void MinimumLength_ShouldNot_Set_Nullable_By_Default()
        {
            // without options. property is nullable, min length is set.
            new SchemaBuilder<TestEntity>()
                .AddRule(entity => entity.TextValue, rule => rule.MinimumLength(1), schema =>
                {
                    schema.Nullable.Should().Be(true);
                    schema.MinLength.Should().Be(1);
                });

            new SchemaBuilder<TestEntity>()
                .ConfigureFVSwaggerGenOptions(options => options.SetNotNullableIfMinLengthGreaterThenZero = false)
                .AddRule(entity => entity.TextValue, rule => rule.MinimumLength(1), schema =>
                {
                    schema.Nullable.Should().Be(true);
                    schema.MinLength.Should().Be(1);
                });

            new SchemaBuilder<TestEntity>()
                .ConfigureFVSwaggerGenOptions(options => options.SetNotNullableIfMinLengthGreaterThenZero = true)
                .AddRule(entity => entity.TextValue, rule => rule.MinimumLength(1), schema =>
                {
                    schema.Nullable.Should().Be(false);
                    schema.MinLength.Should().Be(1);
                });
        }
    }
}
