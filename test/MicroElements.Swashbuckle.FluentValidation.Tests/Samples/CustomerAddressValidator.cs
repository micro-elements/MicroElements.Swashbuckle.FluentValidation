using FluentValidation;
using System.Threading.Tasks;

namespace MicroElements.Swashbuckle.FluentValidation.Tests.Samples;

internal class CustomerAddressValidator : AbstractValidator<Customer>
{
    public CustomerAddressValidator()
    {
        UnlessAsync((customer, _) => Task.FromResult(customer.Surname == "Test"), () =>
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
