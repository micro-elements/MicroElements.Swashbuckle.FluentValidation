using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace SampleWebApi.Contracts
{
    public class Sample
    {
        public string PropertyWithNoRules { get; set; }

        public string NotNull { get; set; }
        public string NotEmpty { get; set; }
        public string EmailAddress { get; set; }
        public string RegexField { get; set; }

        public int ValueInRange { get; set; }
        public int ValueInRangeExclusive { get; set; }

        public float ValueInRangeFloat { get; set; }
        public double ValueInRangeDouble { get; set; }
        public decimal DecimalValue { get; set; }

        // ReSharper disable once InconsistentNaming
        // https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/10
        public string javaStyleProperty { get; set; }
    }

    public class SampleValidator : AbstractValidator<Sample>
    {
        public SampleValidator()
        {
            RuleFor(sample => sample.NotNull).NotNull();
            RuleFor(sample => sample.NotEmpty).NotEmpty();
            RuleFor(sample => sample.EmailAddress).EmailAddress();
            RuleFor(sample => sample.RegexField).Matches(@"(\d{4})-(\d{2})-(\d{2})");

            RuleFor(sample => sample.ValueInRange).GreaterThanOrEqualTo(5).LessThanOrEqualTo(10);
            RuleFor(sample => sample.ValueInRangeExclusive).GreaterThan(5).LessThan(10);

            RuleFor(sample => sample.ValueInRangeFloat).InclusiveBetween(5.1f, 10.2f);
            RuleFor(sample => sample.ValueInRangeDouble).ExclusiveBetween(5.1, 10.2);
            RuleFor(sample => sample.DecimalValue).InclusiveBetween(1.333m, 200.333m);

            RuleFor(sample => sample.javaStyleProperty).MaximumLength(6);
        }
    }

    public class SampleWithDataAnnotations
    {
        public string PropertyWithNoRules { get; set; }

        [Required]
        public string NotNull { get; set; }

        [MinLength(1)]
        public string NotEmpty { get; set; }

        [EmailAddress]
        public string EmailAddress { get; set; }

        [RegularExpression(@"(\d{4})-(\d{2})-(\d{2})")]
        public string RegexField { get; set; }

        [Range(5, 10)]
        public int ValueInRange { get; set; }

        [Range(5.1f, 10.2f)]
        public float ValueInRangeFloat { get; set; }

        [Range(5.1, 10.2)]
        public double ValueInRangeDouble { get; set; }

        [Range(1.333, 200.333)]
        public decimal DecimalValue { get; set; }

        [MaxLength(6)]
        public string javaStyleProperty { get; set; }
    }

    // https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/6
    public class SampleWithNoRequired
    {
        public string PropertyWithNoRules { get; set; }

        public int ValueInRange { get; set; }
    }

    public class SampleWithNoRequiredValidator : AbstractValidator<SampleWithNoRequired>
    {
        public SampleWithNoRequiredValidator()
        {
            RuleFor(sample => sample.ValueInRange).GreaterThanOrEqualTo(5).LessThanOrEqualTo(10);
        }
    }
}