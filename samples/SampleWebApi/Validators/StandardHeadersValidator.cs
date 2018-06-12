using FluentValidation;
using SampleWebApi.Contracts;

namespace SampleWebApi.Validators
{
    public class StandardHeadersValidator : AbstractValidator<StandardHeaders>
    {
        public StandardHeadersValidator()
        {
            RuleFor(x => x.TransactionId)
                .NotNull().WithMessage("Missing TransactionId in header")
                .NotEmpty().WithMessage("Value missing for TransactionId")
                .MinimumLength(8);

            RuleFor(x => x.RequestId)
                .NotNull().WithMessage("Missing RequestId in header")
                .NotEmpty().WithMessage("Value missing for RequestId")
                .Matches(Constants.GuidRegex);
        }
    }
}