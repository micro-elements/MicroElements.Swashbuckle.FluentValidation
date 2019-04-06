using FluentValidation;
using SampleWebApi.Contracts;

namespace SampleWebApi.Validators
{
    public class BasicRequestValidator : AbstractValidator<BasicGetRequest>
    {
        public BasicRequestValidator()
        {
            RuleFor(x => x.StandardHeaders).SetValidator(new StandardHeadersValidator());
            RuleFor(x => x.ValueFromHeader).MaximumLength(10);

            RuleFor(x => x.ValueFromHeader).NotEmpty().WithMessage("Missing value from header");
            RuleFor(x => x.ValueFromQuery).NotEmpty().WithMessage("Missing value from query");
        }
    }
}