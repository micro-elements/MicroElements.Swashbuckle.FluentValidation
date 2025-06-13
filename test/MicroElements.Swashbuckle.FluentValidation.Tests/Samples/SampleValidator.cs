using FluentValidation;
using FluentValidation.Validators;
using JetBrains.Annotations;

namespace MicroElements.Swashbuckle.FluentValidation.Tests.Samples;

[UsedImplicitly]
public class SampleValidator : AbstractValidator<Sample>
{
    public SampleValidator()
    {
        RuleFor(sample => sample.NotNull).NotNull();
        RuleFor(sample => sample.NotEmpty).NotEmpty();
        RuleFor(sample => sample.EmailAddressRegex).EmailAddress(EmailValidationMode.Net4xRegex);
        RuleFor(sample => sample.EmailAddress).EmailAddress();
        RuleFor(sample => sample.RegexField).Matches(@"(\d{4})-(\d{2})-(\d{2})");

        RuleFor(sample => sample.ValueInRange).GreaterThanOrEqualTo(5).LessThanOrEqualTo(10);
        RuleFor(sample => sample.ValueInRangeExclusive).GreaterThan(5).LessThan(10);

        RuleFor(sample => sample.ValueInRangeFloat).InclusiveBetween(5.1f, 10.2f);
        RuleFor(sample => sample.ValueInRangeDouble).ExclusiveBetween(5.1, 10.2);
        RuleFor(sample => sample.DecimalValue).InclusiveBetween(1.333m, 200.333m);

        RuleFor(sample => sample.javaStyleProperty).MaximumLength(6);

        RuleFor(sample => sample.NotEmptyWithMaxLength).NotEmpty().MaximumLength(50);
    }
}
