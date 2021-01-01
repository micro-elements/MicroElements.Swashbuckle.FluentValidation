using System.Collections.Generic;
using FluentValidation;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;

namespace SampleWebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PersonController : Controller
    {
        [HttpPost("[action]")]
        public ActionResult<Person> AddPerson(Person person)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            return person;
        }
    }

    public class Person
    {
        public List<string> Emails { get; set; } = new List<string>();
    }

    [UsedImplicitly]
    public class PersonValidator : AbstractValidator<Person>
    {
        public PersonValidator()
        {
            RuleForEach(x => x.Emails).EmailAddress();
        }
    }
}