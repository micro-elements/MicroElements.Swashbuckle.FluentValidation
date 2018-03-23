using FluentValidation;
using SampleWebApi.Contracts;

namespace SampleWebApi.Validators
{
    public class CustomerValidator : AbstractValidator<Customer>
    {
        public CustomerValidator()
        {
            RuleFor(customer => customer.Surname).NotEmpty();
            RuleFor(customer => customer.Forename).NotEmpty().WithMessage("Please specify a first name");
            RuleFor(customer => customer.Address).Length(20, 250);
        }
    }
}
