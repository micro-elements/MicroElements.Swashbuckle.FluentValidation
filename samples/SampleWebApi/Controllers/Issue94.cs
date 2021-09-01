using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;

namespace SampleWebApi.Controllers
{
    /// <summary>
    /// https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/94
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class Issue94 : Controller
    {
        public class Person94
        {
            public string Name { get; set; }

            public List<string> Emails { get; set; } = new List<string>();

            private string FirstName
            {
                get
                {
                    return this.Name == null ? null :
                        !this.Name.Contains(" ") ? this.Name :
                        this.Name.Split(new char[] { ' ' }).FirstOrDefault();
                }
            }

            private string LastName
            {
                get
                {
                    return this.Name == null ? null :
                        !this.Name.Contains(" ") ? null :
                        this.Name.Split(new char[] { ' ' }).Skip(1).FirstOrDefault();
                }
            }

            [UsedImplicitly]
            public class PersonValidator : AbstractValidator<Person94>
            {
                public PersonValidator()
                {
                    this.RuleFor(x => x.FirstName)
                        .MaximumLength(50)
                        .OverridePropertyName(x => x.Name)
                        .WithName("First Name")
                        ;

                    this.RuleFor(x => x.LastName)
                        .MaximumLength(50)
                        .OverridePropertyName(x => x.Name)
                        .WithName("Last Name")
                        ;

                    this.RuleFor(x => x.Name)
                        .MaximumLength(101);

                    RuleForEach(x => x.Emails).EmailAddress();
                }
            }
        }

        [HttpPost("[action]")]
        public ActionResult<Person94> AddPerson94(Person94 person)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            return person;
        }
    }
}