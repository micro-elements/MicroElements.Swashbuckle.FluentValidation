using System.Threading.Tasks;
using FluentValidation;

namespace MicroElements.Swashbuckle.FluentValidation.Tests.Samples;

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
            .WhenAsync((customer, _) => Task.FromResult(customer.Id == 1))
            .WithMessage("This WILL NOT be in the OpenAPI spec.");

        RuleFor(customer => customer.Surname)
            .NotEmpty();

        RuleFor(customer => customer.Forename)
            .NotEmpty()
            .WithMessage("Please specify a first name");

        Include(new CustomerAddressValidator());
    }
}