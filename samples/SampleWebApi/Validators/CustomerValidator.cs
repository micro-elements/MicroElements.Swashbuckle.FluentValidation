using System.Threading.Tasks;
using FluentValidation;
using SampleWebApi.Contracts;

namespace SampleWebApi.Validators
{
    public class CustomerValidator : AbstractValidator<Customer>
    {
        public CustomerValidator()
        {
            When(customer => customer.Id == 1, () =>
            {
                RuleFor(customer => customer.Discount)
                    .NotEmpty()
                    .WithMessage("This WILL NOT be in the OpenAPI  spec.");
            });

            RuleFor(customer => customer.Discount)
                .ExclusiveBetween(4, 5)
                .WithMessage("This WILL be in the OpenAPI spec.");
            RuleFor(customer => customer.Discount)
                .NotEmpty()
                .WhenAsync((customer, token) => Task.FromResult(customer.Id == 1))
                .WithMessage("This WILL NOT be in the OpenAPI spec.");

            RuleFor(customer => customer.Surname)
                .NotEmpty();
            RuleFor(customer => customer.Forename)
                .NotEmpty()
                .WithMessage("Please specify a first name");

            Include(new CustomerAddressValidator());
        }
    }

    internal class CustomerAddressValidator : AbstractValidator<Customer>
    {
        public CustomerAddressValidator()
        {
            UnlessAsync((customer, token) => Task.FromResult(customer.Surname == "Test"), () =>
            {
                RuleFor(customer => customer.Discount)
                    .NotEmpty()
                    .WithMessage("This WILL NOT be in the OpenAPI spec.");
            });

            RuleFor(customer => customer.Discount)
                .NotEmpty()
                .Unless(customer => customer.Surname == "Test")
                .WithMessage("This WILL NOT be in the OpenAPI spec.");

            RuleFor(customer => customer.Address)
                .Length(20, 250);
        }
    }
}
