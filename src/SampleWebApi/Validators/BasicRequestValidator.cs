using FluentValidation;
using SampleWebApi.Contracts;

namespace SampleWebApi.Validators
{
    public class BasicRequestValidator : AbstractValidator<BasicGetRequest>
    {
        public BasicRequestValidator()
        {
            RuleFor(x => x.StandardHeaders).SetValidator(new StandardHeadersValidator());

            RuleFor(customer => customer.ValueFromHeader).NotEmpty().WithMessage("Missing value from header");
            RuleFor(customer => customer.ValueFromQuery).NotEmpty().WithMessage("Missing value from query");
        }
    }
}