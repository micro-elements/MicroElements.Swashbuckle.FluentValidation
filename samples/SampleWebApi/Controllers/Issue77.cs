using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace SampleWebApi.Controllers
{
    public class Issue77 : Controller
    {
        [HttpGet("[action]")]
        public IActionResult AddBase([FromBody] AbstractInstitutionModel query) => Ok();

        [HttpGet("[action]")]
        public IActionResult AddChild([FromBody] InstitutionModel query) => Ok();


        // abstract
        public abstract class  AbstractInstitutionModel
        {
            public string Name { get; set; }
        }

        // class
        public class InstitutionModel : AbstractInstitutionModel
        {
        }

        // fluent validator
        public class AbstractInstitutionModelValidator : AbstractValidator<AbstractInstitutionModel>
        {
            public AbstractInstitutionModelValidator()
            {
                RuleFor(p => p.Name)
                    .NotEmpty()
                    .MaximumLength(100);
            }
        }
    }
}