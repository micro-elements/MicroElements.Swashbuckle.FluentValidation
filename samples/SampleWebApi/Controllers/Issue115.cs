using System.Collections.Generic;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace SampleWebApi.Controllers
{
    [Route("api/[controller]")]
    public class Issue115 : Controller
    {
        [HttpGet("[action]")]
        public IActionResult AddA([FromBody] ItemA query) => Ok();

        [HttpGet("[action]")]
        public IActionResult AddB([FromBody] ItemB query) => Ok();

        [HttpGet("[action]")]
        public IActionResult AddItem([FromBody] Item query) => Ok();

        [HttpGet("[action]")]
        public IActionResult AddChild([FromBody] Child query) => Ok();

        public abstract class Item
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        public class ItemA : Item
        {
            public class ItemAValidator : AbstractValidator<ItemA>
            {
                public ItemAValidator()
                {
                    this.RuleFor(x => x.Name)
                        .NotEmpty()
                        .Length(1, 50);
                }
            }
        }
        public class ItemB : Item
        {

        }

        public abstract class Person
        {
            public string FirstName { get; set; }
            public string Initials { get; set; }
            public string LastName { get; set; }
        }

        public class Child: Person
        {
            public List<string> Parents { get; set; }
        }

        public class PersonValidator : AbstractValidator<Person>
        {
            public PersonValidator ()
            {
                RuleFor(person => person.LastName ).NotNull();
            }
        }

        public class ChildValidator : AbstractValidator<Child>
        {
            public ChildValidator ()
            {
                //Include(new PersonValidator());
                RuleFor(child => child.FirstName).NotNull();
                RuleFor(child => child.Parents).NotNull();
            }
        }
    }
}