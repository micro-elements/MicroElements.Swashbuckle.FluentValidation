using System.Collections.Generic;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace SampleWebApi.Issue60
{
    public class Address
    {
        public string City { get; set; }
    }

    public class Person
    {
        public string Name { get; set; }
        public Address Address { get; set; }
    }

    public class AddressValidator : AbstractValidator<Address>
    {
        public AddressValidator()
        {
            //RuleFor(x => x.City).NotNull();
            RuleFor(x => x).NotNull();
        }
    }

    public class PersonValidator : AbstractValidator<Person>
    {
        public PersonValidator(AddressValidator addressValidator)
        {
            RuleFor(x => x.Address).SetValidator(addressValidator);
            //RuleFor(x => x.Address).NotNull();
        }
    }

    [Route("api/[controller]")]
    public class Issue60Controller : Controller
    {
        [HttpGet]
        public IEnumerable<Person> Get()
        {
            return new[]
            {
                new Person
                {
                    Name = "Bill Gates",
                    Address = new Address(){City = "NY"}
                }
            };
        }

        [HttpPost("[action]")]
        public IActionResult AddCustomer([FromBody] Person person)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            return Ok();
        }
    }
}
