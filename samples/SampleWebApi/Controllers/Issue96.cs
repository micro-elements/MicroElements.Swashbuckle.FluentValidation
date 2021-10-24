using FluentValidation;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace SampleWebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class Issue96 : Controller
    {
        public class Tenant96
        {
            public Person96 Owner { get; set; }

            [UsedImplicitly]
            public class Tenant96Validator : AbstractValidator<Tenant96>
            {
                public Tenant96Validator()
                {
                    RuleFor(x => x.Owner)
                        .SetValidator(x => new Person96.Person96Validator(x));
                }
            }
        }

        public class Person96
        {
            public Account96 Account { get; set; }

            public class Person96Validator : AbstractValidator<Person96>
            {
                public Person96Validator(Tenant96 tenant)
                {
                    RuleFor(x => x.Account)
                        .SetInheritanceValidator(x =>
                        {
                            x.Add(new StrictAccount96.StrictAccount96Validator(tenant));
                        });
                }
            }
        }

        public class Account96
        {
            public string UserName { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string Password { get; set; }
        }

        public class StrictAccount96 : Account96
        {
            public class StrictAccount96Validator : AbstractValidator<Account96>
            {
                public StrictAccount96Validator(Tenant96 tenant)
                {
                    this.RuleFor(x => x.UserName)
                        .NotEmpty()
                        .MaximumLength(50);

                    this.RuleFor(x => x.Password)
                        .MaximumLength(256);
                }
            }
        }

        [HttpPost("[action]")]
        public ActionResult<Tenant96> AddTenant96(Tenant96 tenant)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            return tenant;
        }
    }
}