using System;
using FluentValidation;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NJsonSchema.Converters;
using Swashbuckle.AspNetCore.Annotations;

namespace SampleWebApi.Controllers
{
    /// <summary>
    /// https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/100
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class Issue100 : Controller
    {
        public class Person100A : Person100
        {
            public const string DiscriminatorValue = nameof(Person100A);

            // There should be real class that will be registered in FluentValidationFactory
            public class Person100AValidator : Person100Validator<Person100A> { }
        }

        public class Person100B : Person100
        {
            public const string DiscriminatorValue = nameof(Person100B);

            public int Children { get; set; }

            [UsedImplicitly]
            public class Person100BValidator : Person100Validator<Person100B>
            {
                public Person100BValidator()
                {
                    this.RuleFor(x => x.Children)
                        .InclusiveBetween(0, 100);
                }
            }
        }

        public abstract class Person100 : BasePerson100
        {
            public Person100()
            {
                this.BirthDate = DateTime.UtcNow;
            }

            public DateTime BirthDate { get; set; }

            public int? Age { get; set; }

            [UsedImplicitly]
            public class Person100Validator<T> : AbstractValidator<T>
                where T : Person100
            {
                public Person100Validator()
                {
                    this.RuleFor(x => x.BirthDate)
                        .InclusiveBetween(new DateTime(1900, 1, 1, 0, 0, 0), new DateTime(2003, 1, 1));
                    this.RuleFor(x => x.Age)
                        .InclusiveBetween(18, 150);
                }
            }
        }

        [SwaggerDiscriminator("discriminator")]
        [SwaggerSubType(typeof(Person100A), DiscriminatorValue = Person100A.DiscriminatorValue)]
        [SwaggerSubType(typeof(Person100B), DiscriminatorValue = Person100B.DiscriminatorValue)]
        [JsonConverter(typeof(JsonInheritanceConverter<BasePerson100>), "discriminator")]
        [JsonInheritance(Person100A.DiscriminatorValue, typeof(Person100A))]
        [JsonInheritance(Person100B.DiscriminatorValue, typeof(Person100B))]
        public abstract class BasePerson100
        {
        }

        public class MyEntity100
        {
            public BasePerson100 Person { get; set; }

            [UsedImplicitly]
            public class MyEntity100Validator : AbstractValidator<MyEntity100>
            {
                public MyEntity100Validator()
                {
                    this.RuleFor(x => x.Person)
                        .SetInheritanceValidator(x =>
                        {
                            x.Add<Person100A>(new Person100A.Person100AValidator());
                            x.Add<Person100B>(new Person100B.Person100BValidator());
                        });
                }
            }
        }

        [HttpPost("[action]")]
        public ActionResult<MyEntity100> AddMyEntity100(MyEntity100 myEntity)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            return myEntity;
        }
    }
}