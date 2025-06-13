using FluentAssertions;
using FluentValidation;
using FluentValidation.Validators;
using MicroElements.OpenApi;
using MicroElements.Swashbuckle.FluentValidation.Tests.Samples;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Xunit;

namespace MicroElements.Swashbuckle.FluentValidation.Tests
{
    public partial class SchemaGenerationTests : UnitTestBase
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
                .ConfigureSchemaGenerationOptions(options => options.SetNotNullableIfMinLengthGreaterThenZero = false)
                .AddRule(entity => entity.TextValue, rule => rule.MinimumLength(1), schema =>
                {
                    schema.Nullable.Should().Be(true);
                    schema.MinLength.Should().Be(1);
                });

            new SchemaBuilder<TestEntity>()
                .ConfigureSchemaGenerationOptions(options => options.SetNotNullableIfMinLengthGreaterThenZero = true)
                .AddRule(entity => entity.TextValue, rule => rule.MinimumLength(1), schema =>
                {
                    schema.Nullable.Should().Be(false);
                    schema.MinLength.Should().Be(1);
                });
        }

        public class BestShot
        {
            [JsonPropertyName("photo")]
            public string Link { get; set; }

            [JsonPropertyName("zone")]
            public string Area { get; set; }
        }

        [Fact]
        public void NameOverrides()
        {
            new SchemaBuilder<BestShot>()
                .AddRule(entity => entity.Link,
                    rule => rule.MinimumLength(5),
                    schema => schema.MinLength.Should().Be(5));
        }

        public class MinMaxLength
        {
            public string Name { get; set; }
            public List<string> Qualities { get; set; }
        }

        public class MinMaxLengthValidator : AbstractValidator<MinMaxLength>
        {
            public MinMaxLengthValidator(int min, int max)
            {
                RuleFor(x => x.Name).MinimumLength(min).MaximumLength(max);
                RuleFor(x => x.Qualities).ListRange(min, max);
            }
        }

        [Theory]
        [InlineData(0, 40)]
        [InlineData(10, 0)]
        [InlineData(3, 40)]
        public void ILengthValidator_ProperlyAppliesMinMax_ToStrings(int min, int max)
        {
            var schemaRepository = new SchemaRepository();
            var referenceSchema = SchemaGenerator(new MinMaxLengthValidator(min, max)).GenerateSchema(typeof(MinMaxLength), schemaRepository);

            var schema = schemaRepository.Schemas[referenceSchema.Reference.Id];

            if (min > 0)
                schema.Properties[nameof(MinMaxLength.Name)].MinLength.Should().Be(min);
            else
                schema.Properties[nameof(MinMaxLength.Name)].MinLength.Should().BeNull();
            if (max > 0)
                schema.Properties[nameof(MinMaxLength.Name)].MaxLength.Should().Be(max);
            else
                schema.Properties[nameof(MinMaxLength.Name)].MaxLength.Should().BeNull();

            // MinItems / MaxItems shoiuld not be set for strings
            schema.Properties[nameof(MinMaxLength.Name)].MinItems.Should().BeNull();
            schema.Properties[nameof(MinMaxLength.Name)].MaxItems.Should().BeNull();
        }

        [Theory]
        [InlineData(0, 40)]
        [InlineData(10, 0)]
        [InlineData(3, 40)]
        public void ILengthValidator_ProperlyAppliesMinMax_ToArrays(int min, int max)
        {
            var schemaRepository = new SchemaRepository();
            var referenceSchema = SchemaGenerator(new MinMaxLengthValidator(min, max)).GenerateSchema(typeof(MinMaxLength), schemaRepository);

            var schema = schemaRepository.Schemas[referenceSchema.Reference.Id];

            // MinLength / MaxLength should not be set for arrays
            schema.Properties[nameof(MinMaxLength.Qualities)].MinLength.Should().BeNull();
            schema.Properties[nameof(MinMaxLength.Qualities)].MaxLength.Should().BeNull();

            if (min > 0)
                schema.Properties[nameof(MinMaxLength.Qualities)].MinItems.Should().Be(min);
            else
                schema.Properties[nameof(MinMaxLength.Qualities)].MinItems.Should().BeNull();
            if (max > 0)
                schema.Properties[nameof(MinMaxLength.Qualities)].MaxItems.Should().Be(max);
            else
                schema.Properties[nameof(MinMaxLength.Qualities)].MaxItems.Should().BeNull();
        }

        [Fact]
        // See the issue https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/156
        public void DerivedSample_ShouldHave_ValidationRulesApplied()
        {
            var schemaRepository = new SchemaRepository();
            var schemaGenerator = SchemaGenerator(options =>
            {
                options.UseAllOfForInheritance = true;
                ConfigureGenerator(options, [new DervidedSampleValidator()]);
            });

            schemaGenerator.GenerateSchema(typeof(BaseSample), schemaRepository);
            var referenceSchema = schemaGenerator.GenerateSchema(typeof(DerivedSample), schemaRepository);
            var schema = schemaRepository.Schemas[referenceSchema.Reference.Id];

            // Should be empty after the change made because of the issue https://github.com/domaindrivendev/Swashbuckle.AspNetCore/issues/3021
            schema.Properties.Should().BeEmpty();
            schema.AllOf.Should().HaveCount(2);

            var derivedSampleSchema = schema.AllOf.FirstOrDefault(s => s.Type == "object");

            Assert.NotNull(derivedSampleSchema);

            derivedSampleSchema.Properties.Should().HaveCount(1);
            var propertySchema = derivedSampleSchema.Properties.First().Value;
            propertySchema.MaxLength.Should().Be(255);
            propertySchema.MinLength.Should().Be(1);

            derivedSampleSchema.Required.Should().HaveCount(1);
            derivedSampleSchema.Required.First().Should().Be("Name");
        }
    }

    public class BaseSample
    {
        public int Id { get; set; }
    }

    public class DerivedSample : BaseSample
    {
        public string? Name { get; set; }
    }

    public class DervidedSampleValidator : AbstractValidator<DerivedSample>
    {
        public DervidedSampleValidator()
        {
            RuleFor(p => p.Name).NotEmpty().MaximumLength(255);
        }
    }

    public static class ValidatorExtensions
    {
        public static IRuleBuilderOptions<T, TProperty> ListRange<T, TProperty>(this IRuleBuilder<T, TProperty> ruleBuilder, int minimumLenghtInclusive, int maximumLengthInclusive)
            where TProperty : IEnumerable
        {
            return ruleBuilder.SetValidator((IPropertyValidator<T, TProperty>)new ListLengthValidator<T, TProperty>(minimumLenghtInclusive, maximumLengthInclusive));
        }

        private sealed class ListLengthValidator<T, TProperty> : PropertyValidator<T, TProperty>, ILengthValidator
        {
            public ListLengthValidator(int minimumLength, int maximumLength)
            {
                Min = minimumLength;
                Max = maximumLength;
            }

            public int Min { get; }

            public int Max { get; }

            public override string Name => nameof(ListLengthValidator<T, TProperty>);

            public override bool IsValid(ValidationContext<T> context, TProperty value)
            {
                if (value is IList listvalue)
                {
                    return listvalue.Count >= this.Min && listvalue.Count <= this.Max;
                }

                return true;
            }

            protected override string GetDefaultMessageTemplate(string errorCode)
            {
                if (this.Min == 0)
                {
                    return $"The number of elements in '{{PropertyName}}' must not exceed '{Max}'.";
                }
                else
                {
                    return $"The number of elements in '{{PropertyName}}' must be between '{Min}' and '{Max}'.";
                }
            }
        }
    }
}
