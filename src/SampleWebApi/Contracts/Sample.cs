using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace SampleWebApi.Contracts
{
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
        public int Value { get; set; }
    }

    public class Sample
    {
        public string PropertyWithNoRules { get; set; }

        public string NotNull { get; set; }
        public string NotEmpty { get; set; }
        public string EmailAddress { get; set; }
        public string RegexField { get; set; }

        public int Value { get; set; }
    }

    public class SampleValidator : AbstractValidator<Sample>
    {
        public SampleValidator()
        {
            RuleFor(sample => sample.NotNull).NotNull();
            RuleFor(sample => sample.NotEmpty).NotEmpty().WithMessage("Please specify a NotEmpty");
            RuleFor(sample => sample.EmailAddress).EmailAddress();
            RuleFor(sample => sample.RegexField).Matches(@"(\d{4})-(\d{2})-(\d{2})");
        }
    }
}