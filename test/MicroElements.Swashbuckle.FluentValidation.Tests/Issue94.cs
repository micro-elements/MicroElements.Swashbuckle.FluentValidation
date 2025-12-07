using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using FluentValidation;
#if OPENAPI_V2
using Microsoft.OpenApi;
#else
using Microsoft.OpenApi.Models;
#endif
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace MicroElements.Swashbuckle.FluentValidation.Tests
{
    /// <summary>
    /// https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/94
    /// </summary>
    public class Issue94 : UnitTestBase
    {
        public class Person
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

            //[UsedImplicitly]
            public class PersonValidator : AbstractValidator<Person>
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

        [Fact]
        public void all_rules_should_be_applied()
        {
            var schemaRepository = new SchemaRepository();
            var referenceSchema = SchemaGenerator(new Person.PersonValidator())
                .GenerateSchema(typeof(Person), schemaRepository);

            var schema = schemaRepository.GetSchema(referenceSchema.GetRefId()!);
            schema.Properties.Keys.Should().BeEquivalentTo("Name", "Emails");
            var nameProperty = schema.GetProperty(nameof(Person.Name))!;
            nameProperty.GetTypeString().Should().Be("string");
            nameProperty.MaxLength.Should().Be(101);
        }
    }
}